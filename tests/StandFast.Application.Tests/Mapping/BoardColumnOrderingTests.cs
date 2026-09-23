using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Mapping;

public sealed class BoardColumnOrderingTests
{
    private static readonly DateTimeOffset NineAm = new(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Roster_IsAlphabeticalByDisplayNameRegardlessOfRosterPosition()
    {
        BoardParticipantDto[] participants =
        [
            Participant("Zoe Brown", displayOrder: 10),
            Participant("adam smith", displayOrder: 20),
            Participant("Mary Jones", displayOrder: 30),
        ];

        IReadOnlyList<BoardParticipantDto> ordered = participants.InColumnOrder(AttendanceState.Roster);

        Assert.Equal(["adam smith", "Mary Jones", "Zoe Brown"], ordered.Select(participant => participant.DisplayName));
    }

    [Fact]
    public void Available_IsAlphabeticalLikeTheRosterRegardlessOfWhenEachPersonArrived()
    {
        BoardParticipantDto[] participants =
        [
            Participant("Zoe Brown", markedAvailableUtc: NineAm),
            Participant("adam smith", markedAvailableUtc: NineAm.AddMinutes(6)),
            Participant("Mary Jones", markedAvailableUtc: NineAm.AddMinutes(2)),
        ];

        IReadOnlyList<BoardParticipantDto> ordered = participants.InColumnOrder(AttendanceState.Available);

        Assert.Equal(["adam smith", "Mary Jones", "Zoe Brown"], ordered.Select(participant => participant.DisplayName));
    }

    [Fact]
    public void Presented_IsOrderedByWhenEachPersonPresented()
    {
        BoardParticipantDto[] participants =
        [
            Participant("Second", presentedUtc: NineAm.AddMinutes(4)),
            Participant("First", presentedUtc: NineAm.AddMinutes(1)),
            Participant("Third", presentedUtc: NineAm.AddMinutes(9)),
        ];

        IReadOnlyList<BoardParticipantDto> ordered = participants.InColumnOrder(AttendanceState.Presented);

        Assert.Equal(["First", "Second", "Third"], ordered.Select(participant => participant.DisplayName));
    }

    [Fact]
    public void Presented_FallsBackToNameWhenTwoEntriesShareATimestamp()
    {
        BoardParticipantDto[] participants =
        [
            Participant("Bob", presentedUtc: NineAm),
            Participant("Alice", presentedUtc: NineAm),
        ];

        IReadOnlyList<BoardParticipantDto> ordered = participants.InColumnOrder(AttendanceState.Presented);

        Assert.Equal(["Alice", "Bob"], ordered.Select(participant => participant.DisplayName));
    }

    private static BoardParticipantDto Participant(
        string displayName,
        int displayOrder = 0,
        DateTimeOffset? markedAvailableUtc = null,
        DateTimeOffset? presentedUtc = null) =>
        new(Guid.CreateVersion7(), displayName, "XX", $"{displayName}@example.com", displayOrder, AttendanceState.Roster,
            markedAvailableUtc, presentedUtc, null, null, null, null, null, null, null);
}
