using FluentValidation;
using StandFast.Application.Auditing;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

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

    public async Task<IReadOnlyList<StandupMemberDto>> GetMembersAsync(Guid standupId, RosterRole role, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StandupMember> members = await standups.GetMembersAsync(standupId, role, cancellationToken);
        Dictionary<Guid, Person> peopleById = (await people.GetAllAsync(cancellationToken)).ToDictionary(person => person.Id);

        return members.ToRoster(peopleById);
    }

    public async Task<StandupNamesByPersonDto> GetStandupNamesByPersonAsync(CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<Standup>> standupTask = standups.GetAllAsync(cancellationToken);
        Task<IReadOnlyList<StandupMember>>[] memberTasks = [.. RosterRoleExtensions.InDisplayOrder.Select(role => standups.GetAllMembersAsync(role, cancellationToken))];
        await Task.WhenAll([standupTask, .. memberTasks]);

        Dictionary<Guid, string> namesById = standupTask.Result.ToDictionary(standup => standup.Id, standup => standup.Name);

        return new StandupNamesByPersonDto(RosterRoleExtensions.InDisplayOrder
            .Select((role, index) => (Role: role, Memberships: memberTasks[index].Result))
            .ToDictionary(item => item.Role, item => NamesByPerson(item.Memberships, namesById)));
    }

    public async Task AddMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StandupMember> existing = await standups.GetMembersAsync(standupId, role, cancellationToken);
        StandupMember? member = existing.FirstOrDefault(candidate => candidate.PersonId == personId);
        int nextOrder = existing.Count == 0 ? AppendedMemberOrderStep : existing.Max(candidate => candidate.DisplayOrder) + AppendedMemberOrderStep;

        member ??= new StandupMember { StandupId = standupId, PersonId = personId, Role = role, DisplayOrder = nextOrder, CreatedUtc = clock.UtcNow };
        member.IsActive = true;

        await standups.UpsertMemberAsync(member, cancellationToken);
        audit.Record(AuditEvents.MemberAdded, MemberAuditTargetType, MemberTargetId(standupId, personId, role), $"Added to the {role} roster.");
    }

    public async Task RemoveMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken = default)
    {
        await standups.RemoveMemberAsync(standupId, personId, role, cancellationToken);
        audit.Record(AuditEvents.MemberRemoved, MemberAuditTargetType, MemberTargetId(standupId, personId, role), $"Removed from the {role} roster.");
    }

    public async Task SetMemberOrderAsync(Guid standupId, Guid personId, RosterRole role, int displayOrder, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StandupMember> members = await standups.GetMembersAsync(standupId, role, cancellationToken);
        StandupMember? member = members.FirstOrDefault(candidate => candidate.PersonId == personId);
        if (member is null)
        {
            return;
        }

        member.DisplayOrder = displayOrder;
        await standups.UpsertMemberAsync(member, cancellationToken);
    }

    /// <summary>Standup names per person for one role's memberships. A membership whose standup has gone is dropped, which is what an interrupted delete leaves behind.</summary>
    private static IReadOnlyDictionary<Guid, IReadOnlyList<string>> NamesByPerson(IReadOnlyList<StandupMember> memberships, IReadOnlyDictionary<Guid, string> namesById) =>
        memberships
            .Where(member => member.IsActive && namesById.ContainsKey(member.StandupId))
            .GroupBy(member => member.PersonId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(member => namesById[member.StandupId]).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)]);

    private static string MemberTargetId(Guid standupId, Guid personId, RosterRole role) => $"{standupId}/{personId}/{role}";
}
