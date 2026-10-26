namespace StandFast.Domain.Common;

/// <summary>The rule that following supervisors upward from anyone always ends at someone with no supervisor, never back where it started.</summary>
public static class SupervisorChain
{
    /// <summary>
    /// The loop reached by walking up from <paramref name="personId"/> with <paramref name="supervisorId"/> as their supervisor, as the people in it in reporting order
    /// with the first repeated at the end. Empty when the chain ends. A supervisor missing from <paramref name="supervisorOf"/> ends the chain.
    /// </summary>
    public static IReadOnlyList<Guid> FindLoop(Guid personId, Guid? supervisorId, IReadOnlyDictionary<Guid, Guid?> supervisorOf)
    {
        List<Guid> chain = [personId];

        for (Guid? next = supervisorId; next is { } current; next = supervisorOf.GetValueOrDefault(current))
        {
            int repeatedAt = chain.IndexOf(current);
            if (repeatedAt >= 0)
            {
                return [.. chain[repeatedAt..], current];
            }

            chain.Add(current);
        }

        return [];
    }
}
