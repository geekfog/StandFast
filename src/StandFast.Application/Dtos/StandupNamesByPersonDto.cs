using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

/// <summary>
/// The standups each person appears on, grouped by the role they hold there. Every role travels in one object so a screen showing a column per role
/// fills them all from a single call.
/// </summary>
public sealed record StandupNamesByPersonDto(IReadOnlyDictionary<RosterRole, IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ByRole)
{
    private static readonly IReadOnlyList<string> NoStandups = [];

    public static readonly StandupNamesByPersonDto Empty = new(new Dictionary<RosterRole, IReadOnlyDictionary<Guid, IReadOnlyList<string>>>());

    /// <summary>Names of the standups this person holds <paramref name="role"/> on, alphabetically, or an empty list when they hold it nowhere.</summary>
    public IReadOnlyList<string> For(RosterRole role, Guid personId) =>
        ByRole.TryGetValue(role, out IReadOnlyDictionary<Guid, IReadOnlyList<string>>? byPerson) && byPerson.TryGetValue(personId, out IReadOnlyList<string>? names) ? names : NoStandups;
}
