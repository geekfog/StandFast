using StandFast.Application.Dtos;

namespace StandFast.Application.Mapping;

public static class RosterOrdering
{
    /// <summary>The single roster sort used by the board columns and the membership screen.</summary>
    public static IReadOnlyList<T> InRosterOrder<T>(this IEnumerable<T> source) where T : IRosterOrdered =>
        [.. source.OrderBy(item => item.DisplayOrder).ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)];
}
