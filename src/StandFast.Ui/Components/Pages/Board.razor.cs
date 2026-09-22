using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Application.Services;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Enums;
using StandFast.Ui.Common;
using StandFast.Ui.Configuration;

namespace StandFast.Ui.Components.Pages;

public partial class Board
{
    /// <summary>Column order across the board, left to right. Driving the markup from this keeps the columns and the tap transitions in step.</summary>
    private static readonly AttendanceState[] BoardColumns = [AttendanceState.Roster, AttendanceState.Available, AttendanceState.Presented];

    private IReadOnlyList<StandupDto> standups = [];
    private StandupBoardDto? board;
    private IReadOnlyCollection<DateOnly> presentedDates = [];
    private TimeZoneInfo timeZone = TimeZoneInfo.Local;
    private DateOnly today;
    private Guid? selectedPersonId;
    private Guid loadedStandupId;
    private DateOnly loadedDate;

    /// <summary>The signed-in user's settings, loaded once by the layout. The board uses them to mark whichever card is the reader's own.</summary>
    [CascadingParameter]
    private UserPreferencesDto Preferences { get; set; } = UserPreferencesDto.Default;

    [Inject]
    private IStandupService StandupService { get; set; } = default!;

    [Inject]
    private IBoardService BoardService { get; set; } = default!;

    [Inject]
    private IClock Clock { get; set; } = default!;

    [Inject]
    private IOptions<StandFastUiOptions> UiOptions { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [SupplyParameterFromQuery(Name = UiRoutes.StandupQueryKey)]
    private Guid? StandupIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = UiRoutes.DateQueryKey)]
    private string? DateQuery { get; set; }

    private Guid SelectedStandupId => StandupIdQuery ?? standups.FirstOrDefault()?.Id ?? Guid.Empty;

    private DateOnly SelectedDate => MeetingCalendar.ParseRouteValue(DateQuery) ?? today;

    private MeetingDays SelectedStandupDays => standups.FirstOrDefault(standup => standup.Id == SelectedStandupId)?.RecurrenceDays ?? MeetingDays.EveryDay;

    private BoardParticipantDto? SelectedParticipant => board?.Participants.FirstOrDefault(participant => participant.PersonId == selectedPersonId);

    private IReadOnlyList<StandupMemberDto> LeaderCandidates => board?.Leaders ?? [];

    /// <summary>Says why the leader picker is empty, since an empty dropdown on its own reads as a standup with nobody available to lead.</summary>
    private string? LeaderHelperText => LeaderCandidates.Count == 0 ? $"Nobody is on the {RosterLabels.RosterTitle(RosterRole.Leader).ToLowerInvariant()} for this standup." : null;

    protected override async Task OnInitializedAsync()
    {
        timeZone = UiOptions.Value.ResolveTimeZone();
        today = Clock.Today(timeZone);
        standups = await StandupService.GetSelectableAsync();
    }

    // The standup and date live in the query string, so the board is reloaded from the URL rather than from component state. That makes any board view shareable.
    protected override async Task OnParametersSetAsync()
    {
        if (SelectedStandupId == Guid.Empty || (SelectedStandupId == loadedStandupId && SelectedDate == loadedDate && board is not null))
        {
            return;
        }

        loadedStandupId = SelectedStandupId;
        loadedDate = SelectedDate;

        IReadOnlyList<DateOnly> week = MeetingCalendar.Week(SelectedDate);
        (board, presentedDates) = (
            await BoardService.GetBoardAsync(SelectedStandupId, SelectedDate),
            await BoardService.GetPresentedDatesAsync(SelectedStandupId, week[0], week[^1]));
    }

    private IReadOnlyList<BoardParticipantDto> ParticipantsIn(AttendanceState state) =>
        board is null ? [] : board.Participants.Where(participant => participant.State == state).InColumnOrder(state);

    private static string EmptyColumnText(AttendanceState state) => state switch
    {
        AttendanceState.Available => "Tap a name on the roster as you spot them in the meeting.",
        AttendanceState.Presented => "Tap a name again once they have given their update.",
        _ => "Everyone has been marked present.",
    };

    private Task OnStandupChangedAsync(Guid standupId) => NavigateAsync(standupId, SelectedDate);

    private Task OnDateChangedAsync(DateOnly date) => NavigateAsync(SelectedStandupId, date);

    private Task NavigateAsync(Guid standupId, DateOnly date)
    {
        selectedPersonId = null;
        Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [UiRoutes.StandupQueryKey] = standupId,
            [UiRoutes.DateQueryKey] = MeetingCalendar.ToRouteValue(date),
        }));

        return Task.CompletedTask;
    }

    private async Task OnLeaderChangedAsync(Guid? personId)
    {
        if (board is null || !await BoardService.SetLeaderAsync(SelectedStandupId, SelectedDate, personId))
        {
            return;
        }

        board = board with { LeaderPersonId = personId };
    }

    private void SelectParticipant(Guid personId) => selectedPersonId = selectedPersonId == personId ? null : personId;

    private void CloseUpdate() => selectedPersonId = null;

    private Task AdvanceAsync(Guid personId) => ApplyAsync(() => BoardService.AdvanceAsync(SelectedStandupId, SelectedDate, personId));

    private Task RevertAsync(Guid personId) => ApplyAsync(() => BoardService.RevertAsync(SelectedStandupId, SelectedDate, personId));

    private Task SaveUpdateAsync(ParticipantUpdateDto update) => ApplyAsync(async () =>
    {
        BoardParticipantDto? saved = await BoardService.SaveUpdateAsync(update);
        Snackbar.Add("Update saved.", Severity.Success);
        return saved;
    });

    /// <summary>Runs a board mutation and swaps the single changed participant into the loaded board, rather than refetching every roster entry.</summary>
    private async Task ApplyAsync(Func<Task<BoardParticipantDto?>> mutation)
    {
        BoardParticipantDto? updated = await mutation();
        if (updated is null || board is null)
        {
            return;
        }

        board = board with { Participants = [.. board.Participants.Select(participant => participant.PersonId == updated.PersonId ? updated : participant)] };
        SyncPresentedMarker();
    }

    /// <summary>Holds the week strip's marker for the selected date in step with the board after a tap, so the week does not have to be refetched.</summary>
    private void SyncPresentedMarker()
    {
        bool hasPresented = board?.Participants.Any(participant => participant.State == AttendanceState.Presented) == true;
        if (hasPresented == presentedDates.Contains(SelectedDate))
        {
            return;
        }

        presentedDates = hasPresented
            ? [.. presentedDates, SelectedDate]
            : [.. presentedDates.Where(date => date != SelectedDate)];
    }
}
