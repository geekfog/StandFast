using StandFast.Domain.Entities;

namespace StandFast.Domain.Common;

/// <summary>Finds the person in People a signed-in user is, which is what the board's star marks and what owns a task list.</summary>
public static class PersonMatch
{
    /// <summary>The person with the address, preferring an active one should the address have been recorded twice. Null when nobody has it.</summary>
    public static Person? ByEmail(IEnumerable<Person> people, string? email) =>
        people.Where(person => EmailAddress.Matches(person.Email, email)).OrderByDescending(person => person.IsActive).FirstOrDefault();
}
