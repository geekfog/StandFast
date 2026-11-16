using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

/// <summary>
/// The standups each person appears on, grouped by the role they hold there. Every role travels in one object so a screen showing a column per role
/// fills them all from a single call.
/// </summary>
public sealed record StandupNamesByPersonDto(IReadOnlyDictionary<RosterRole, IReadOnlyDictionary<Guid, IReadOnlyList<StandupRefDto>>> ByRole)
{
    private static readonly IReadOnlyList<StandupRefDto> NoStandups = [];

    public static readonly StandupNamesByPersonDto Empty = new(new Dictionary<RosterRole, IReadOnlyDictionary<Guid, IReadOnlyList<StandupRefDto>>>());

    /// <summary>The standups this person holds <paramref name="role"/> on, alphabetically by name, or an empty list when they hold it nowhere.</summary>
    public IReadOnlyList<StandupRefDto> For(RosterRole role, Guid personId) =>
        ByRole.TryGetValue(role, out IReadOnlyDictionary<Guid, IReadOnlyList<StandupRefDto>>? byPerson) && byPerson.TryGetValue(personId, out IReadOnlyList<StandupRefDto>? standups)
            ? standups
            : NoStandups;

    /// <summary>
    /// The roles this person holds in display order, on <paramref name="standupId"/> when given, otherwise on any standup. Empty when they are on no
    /// roster in that scope.
    /// </summary>
    public IReadOnlyList<RosterRole> RolesFor(Guid personId, Guid? standupId = null) =>
        [.. RosterRoleExtensions.InDisplayOrder.Where(role => For(role, personId).Any(standup => standupId is null || standup.Id == standupId))];
}
