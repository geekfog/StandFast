namespace StandFast.Application.Dtos;

/// <summary>Implemented by anything shown in roster order, so the sort rule (explicit order, then name) exists once.</summary>
public interface IRosterOrdered
{
    int DisplayOrder { get; }

    string DisplayName { get; }
}
