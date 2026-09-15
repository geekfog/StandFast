using FluentValidation;
using StandFast.Application.Auditing;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Abstractions;
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

    public async Task<PersonEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await people.GetAsync(id, cancellationToken))?.ToEditDto();

    public async Task<Guid> SaveAsync(PersonEditDto person, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(person, cancellationToken);

        Person? existing = person.Id is { } id ? await people.GetAsync(id, cancellationToken) : null;
        bool isNew = existing is null;
        Person entity = existing ?? new Person { CreatedUtc = clock.UtcNow };

        person.ApplyTo(entity);
        entity.ModifiedUtc = isNew ? null : clock.UtcNow;

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

        await people.DeleteAsync(id, cancellationToken);
        audit.Record(AuditEvents.PersonDeleted, AuditTargetType, id.ToString(), $"{existing.DisplayName} <{existing.Email}>");
    }
}
