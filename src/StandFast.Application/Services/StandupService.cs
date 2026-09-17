using FluentValidation;
using StandFast.Application.Auditing;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;

namespace StandFast.Application.Services;

public sealed class StandupService(IStandupRepository standups, IPersonRepository people, IClock clock, IValidator<StandupEditDto> validator, IAuditLog audit) : IStandupService
{
    private const string AuditTargetType = nameof(Standup);
    private const string MemberAuditTargetType = nameof(StandupMember);

    /// <summary>New members land at the end of the roster; the roster sort then falls back to display name for ties.</summary>
    private const int AppendedMemberOrderStep = 10;

    public async Task<IReadOnlyList<StandupDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Standup> stored = await standups.GetAllAsync(cancellationToken);
        return [.. stored.Select(standup => standup.ToDto()).OrderBy(standup => standup.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyList<StandupDto>> GetSelectableAsync(CancellationToken cancellationToken = default) =>
        [.. (await GetAllAsync(cancellationToken)).Where(standup => standup.IsActive)];

    public async Task<StandupEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await standups.GetAsync(id, cancellationToken))?.ToEditDto();

    public async Task<Guid> SaveAsync(StandupEditDto standup, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(standup, cancellationToken);

        Standup? existing = standup.Id is { } id ? await standups.GetAsync(id, cancellationToken) : null;
        bool isNew = existing is null;
        Standup entity = existing ?? new Standup { CreatedUtc = clock.UtcNow };

        standup.ApplyTo(entity);
        entity.ModifiedUtc = isNew ? null : clock.UtcNow;

        await standups.UpsertAsync(entity, cancellationToken);
        audit.Record(isNew ? AuditEvents.StandupCreated : AuditEvents.StandupUpdated, AuditTargetType, entity.Id.ToString(), entity.Name);

        return entity.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Standup? existing = await standups.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        await standups.DeleteAsync(id, cancellationToken);
        audit.Record(AuditEvents.StandupDeleted, AuditTargetType, id.ToString(), existing.Name);
    }

    public async Task<IReadOnlyList<StandupMemberDto>> GetMembersAsync(Guid standupId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StandupMember> members = await standups.GetMembersAsync(standupId, cancellationToken);
        Dictionary<Guid, Person> peopleById = (await people.GetAllAsync(cancellationToken)).ToDictionary(person => person.Id);

        return members
            .Where(member => peopleById.ContainsKey(member.PersonId))
            .Select(member => member.ToDto(peopleById[member.PersonId]))
            .InRosterOrder();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetStandupNamesByPersonAsync(CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<Standup>> standupTask = standups.GetAllAsync(cancellationToken);
        Task<IReadOnlyList<StandupMember>> memberTask = standups.GetAllMembersAsync(cancellationToken);
        await Task.WhenAll(standupTask, memberTask);

        Dictionary<Guid, string> namesById = standupTask.Result.ToDictionary(standup => standup.Id, standup => standup.Name);

        return memberTask.Result
            .Where(member => member.IsActive && namesById.ContainsKey(member.StandupId))
            .GroupBy(member => member.PersonId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(member => namesById[member.StandupId]).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)]);
    }

    public async Task AddMemberAsync(Guid standupId, Guid personId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StandupMember> existing = await standups.GetMembersAsync(standupId, cancellationToken);
        StandupMember? member = existing.FirstOrDefault(candidate => candidate.PersonId == personId);
        int nextOrder = existing.Count == 0 ? AppendedMemberOrderStep : existing.Max(candidate => candidate.DisplayOrder) + AppendedMemberOrderStep;

        member ??= new StandupMember { StandupId = standupId, PersonId = personId, DisplayOrder = nextOrder, CreatedUtc = clock.UtcNow };
        member.IsActive = true;

        await standups.UpsertMemberAsync(member, cancellationToken);
        audit.Record(AuditEvents.MemberAdded, MemberAuditTargetType, $"{standupId}/{personId}", "Added to standup roster.");
    }

    public async Task RemoveMemberAsync(Guid standupId, Guid personId, CancellationToken cancellationToken = default)
    {
        await standups.RemoveMemberAsync(standupId, personId, cancellationToken);
        audit.Record(AuditEvents.MemberRemoved, MemberAuditTargetType, $"{standupId}/{personId}", "Removed from standup roster.");
    }

    public async Task SetMemberOrderAsync(Guid standupId, Guid personId, int displayOrder, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StandupMember> members = await standups.GetMembersAsync(standupId, cancellationToken);
        StandupMember? member = members.FirstOrDefault(candidate => candidate.PersonId == personId);
        if (member is null)
        {
            return;
        }

        member.DisplayOrder = displayOrder;
        await standups.UpsertMemberAsync(member, cancellationToken);
    }
}
