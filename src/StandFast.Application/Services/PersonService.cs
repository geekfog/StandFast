using FluentValidation;
using FluentValidation.Results;
using StandFast.Application.Auditing;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Application.Validation;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Services;

public sealed class PersonService(IPersonRepository people, IClock clock, IValidator<PersonEditDto> validator, IAuditLog audit) : IPersonService
{
    private const string AuditTargetType = nameof(Person);

    public async Task<IReadOnlyList<PersonDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Person> stored = await people.GetAllAsync(cancellationToken);
        return [.. stored.Select(person => person.ToDto()).OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<PersonEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Person? person = await people.GetAsync(id, cancellationToken);
        if (person is null)
        {
            return null;
        }

        PersonEditDto edit = person.ToEditDto();
        edit.Photo = person.PhotoSavedUtc is null ? null : await GetPhotoAsync(id, cancellationToken);
        return edit;
    }

    public async Task<PersonPhotoDto?> GetPhotoAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await people.GetPhotoAsync(id, cancellationToken))?.ToDto();

    public async Task<Guid> SaveAsync(PersonEditDto person, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(person, cancellationToken);

        Person? existing = person.Id is { } id ? await people.GetAsync(id, cancellationToken) : null;
        bool isNew = existing is null;
        Person entity = existing ?? new Person { CreatedUtc = clock.UtcNow };

        await EnsureSupervisorChainEndsAsync(entity.Id, person.SupervisorId, cancellationToken);
        person.ApplyTo(entity);
        entity.ModifiedUtc = isNew ? null : clock.UtcNow;

        // The photo is written before the person, so a person row never points at a photo that is not there yet.
        if (person.IsPhotoChanged)
        {
            if (person.Photo is null)
            {
                await people.DeletePhotoAsync(entity.Id, cancellationToken);
                entity.PhotoSavedUtc = null;
            }
            else
            {
                await people.UpsertPhotoAsync(person.Photo.ToEntity(entity.Id), cancellationToken);
                entity.PhotoSavedUtc = clock.UtcNow;
            }
        }

        await people.UpsertAsync(entity, cancellationToken);
        audit.Record(isNew ? AuditEvents.PersonCreated : AuditEvents.PersonUpdated, AuditTargetType, entity.Id.ToString(), $"{entity.DisplayName} <{entity.Email}>");

        return entity.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Person? existing = await people.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        // Their direct reports are left with no supervisor rather than pointing at someone who is no longer in People.
        foreach (Person report in (await people.GetAllAsync(cancellationToken)).Where(person => person.SupervisorId == id))
        {
            report.SupervisorId = null;
            report.ModifiedUtc = clock.UtcNow;
            await people.UpsertAsync(report, cancellationToken);
            audit.Record(AuditEvents.PersonUpdated, AuditTargetType, report.Id.ToString(), $"{report.DisplayName} <{report.Email}>");
        }

        await people.DeleteAsync(id, cancellationToken);
        audit.Record(AuditEvents.PersonDeleted, AuditTargetType, id.ToString(), $"{existing.DisplayName} <{existing.Email}>");
    }

    /// <summary>Refuses a supervisor who is not in People, or one whose chain upward, with this save applied, loops anywhere rather than ending.</summary>
    private async Task EnsureSupervisorChainEndsAsync(Guid personId, Guid? supervisorId, CancellationToken cancellationToken)
    {
        if (supervisorId is not { } supervisor)
        {
            return;
        }

        Dictionary<Guid, Person> peopleById = (await people.GetAllAsync(cancellationToken)).ToDictionary(person => person.Id);
        Dictionary<Guid, Guid?> supervisorOf = peopleById.Values.ToDictionary(person => person.Id, person => person.SupervisorId);
        supervisorOf[personId] = supervisor;

        string? failure = !peopleById.ContainsKey(supervisor)
            ? ValidationMessages.SupervisorNotFound
            : SupervisorChain.FindLoop(personId, supervisor, supervisorOf) is { Count: > 0 } loop
                ? ValidationMessages.SupervisorLoop(loop.Select(id => peopleById[id].DisplayName))
                : null;

        if (failure is not null)
        {
            throw new ValidationException([new ValidationFailure(nameof(PersonEditDto.SupervisorId), failure)]);
        }
    }
}
