namespace StandFast.Application.Dtos;

/// <summary>A person's membership in a standup, flattened with the person details the roster screen needs.</summary>
public sealed record StandupMemberDto(Guid StandupId, Guid PersonId, string DisplayName, string Email, int DisplayOrder, bool IsActive) : IRosterOrdered;
