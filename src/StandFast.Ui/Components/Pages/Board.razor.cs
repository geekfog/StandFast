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
using StandFast.Ui.Components.Shared;
using StandFast.Ui.Configuration;
using StandFast.Ui.Services;

namespace StandFast.Ui.Components.Pages;

public partial class Board
{
    /// <summary>
    /// Columns across the board, left to right, each listing the states it holds from the top. Leaders and guests sit under the people present, since
    /// all of them are here, and move on to presented only where the standup lets them. Driving the markup from this keeps the columns and the tap transitions in step.
    /// </summary>
    private static readonly AttendanceState[][] BoardColumns =
        [[AttendanceState.Roster], [AttendanceState.Available, AttendanceState.Leader, AttendanceState.Guest], [AttendanceState.Presented]];

    private IReadOnlyList<StandupDto> standups = [];
    private StandupBoardDto? board;
    private IReadOnlyCollection<DateOnly> presentedDates = [];
    private IReadOnlyCollection<DateOnly> lockedDates = [];

    /// <summary>The selected standup's time zone, which decides the meeting date, which date is today, and when the meeting starts.</summary>
    private TimeZoneInfo standupTimeZone = TimeZoneInfo.Local;

    /// <summary>The viewer's browser time zone, or null until it is read or when it cannot be.</summary>
    private TimeZoneInfo? browserTimeZone;

    /// <summary>Short name of <see cref="DisplayTimeZone"/> on the meeting date, such as CST, or null when the browser has none. Valid for <see cref="abbreviatedFor"/> only.</summary>
    private string? displayTimeZoneAbbreviation;
    private (string ZoneId, DateTimeOffset Instant)? abbreviatedFor;

    private DateOnly today;
    private Guid? selectedPersonId;
    private ParticipantUpdatePanel? updatePanel;

    /// <summary>
    /// Who the roster column lists, where null lists everyone. Presenters tapped from it become present, leaders land in Leaders, and guests in Guests;
    /// someone on several rosters is listed once, under the first of presenter, leader, then guest.
    /// </summary>
    private AttendeeKind? rosterView;

    /// <summary>The roster filter's options: everyone, then each kind of attendee.</summary>
    private static readonly IReadOnlyList<AttendeeKind?> RosterViews = [null, .. AttendeeKindExtensions.InDisplayOrder.Cast<AttendeeKind?>()];

    /// <summary>What the standup picker shows. It follows the URL, and holds a new choice only while unsaved edits are confirmed, so a declined switch puts the picker back.</summary>
    private Guid standupPickerValue;
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
    private BrowserTimeZone BrowserTimeZone { get; set; } = default!;

    [Inject]
    private IOptions<StandFastUiOptions> UiOptions { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = UiRoutes.StandupQueryKey)]
    private Guid? StandupIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = UiRoutes.DateQueryKey)]
    private string? DateQuery { get; set; }

    private Guid SelectedStandupId => StandupIdQuery ?? standups.FirstOrDefault()?.Id ?? Guid.Empty;

    private DateOnly SelectedDate => MeetingCalendar.ParseRouteValue(DateQuery) ?? today;

    private StandupDto? SelectedStandup => standups.FirstOrDefault(standup => standup.Id == SelectedStandupId);

    private MeetingDays SelectedStandupDays => SelectedStandup?.RecurrenceDays ?? MeetingDays.EveryDay;

    /// <summary>When the selected standup starts on the selected date. Each card's time zone offset is measured at this instant, so daylight saving on that date counts.</summary>
    private DateTimeOffset MeetingStart => TimeZoneIds.ToInstant(SelectedDate, SelectedStandup?.StartTimeLocal ?? TimeOnly.MinValue, standupTimeZone);

    private BoardParticipantDto? SelectedParticipant => board?.Participants.FirstOrDefault(participant => participant.PersonId == selectedPersonId);

    private IReadOnlyList<StandupMemberDto> LeaderCandidates => board?.LeaderCandidates ?? [];

    /// <summary>Says why the leader picker is empty, shown as its tooltip, since an empty dropdown on its own reads as a standup with nobody available to lead.</summary>
    private string? LeaderEmptyReason => LeaderCandidates.Count > 0 ? null : AnyAttendeeCanLead
        ? "Mark someone as here to pick them."
        : board?.Leaders.Count > 0
            ? $"Mark someone on the {LeaderRosterName} as here to pick them."
            : $"Nobody is on the {LeaderRosterName} for this standup. Turn on picking anyone attending to choose someone else.";

    private bool AnyAttendeeCanLead => board?.AnyAttendeeCanLead == true;

    private string LeaderPoolDescription => AnyAttendeeCanLead
        ? $"Anyone attending can lead on this date. Turn off to offer only the {LeaderRosterName}."
        : $"Only the {LeaderRosterName} can lead on this date. Turn on to offer anyone attending.";

    private static string LeaderRosterName => RosterLabels.RosterTitle(RosterRole.Leader).ToLowerInvariant();

    /// <summary>The time zone the board's times of day are shown in: the browser's when known, otherwise the standup's.</summary>
    private TimeZoneInfo DisplayTimeZone => browserTimeZone ?? standupTimeZone;

    private (string ZoneId, DateTimeOffset Instant) DisplayTimeZoneKey => (TimeZoneIds.ToStored(DisplayTimeZone.Id), MeetingStart);

    /// <summary>The shown time zone's short name, such as CST or CDT on the meeting date, or its IANA id when the browser has no short name for it.</summary>
    private string DisplayTimeZoneLabel => abbreviatedFor == DisplayTimeZoneKey && displayTimeZoneAbbreviation is { } abbreviation ? abbreviation : DisplayTimeZoneKey.ZoneId;

    private string DisplayTimeZoneDescription => browserTimeZone is null
        ? $"Times are shown in the standup's time zone, {DescribeTimeZone(standupTimeZone)}, because your browser's time zone is not available."
        : $"Times are shown in your browser's time zone, {DescribeTimeZone(browserTimeZone)}. The meeting date follows the standup's time zone, {DescribeTimeZone(standupTimeZone)}.";

    private static string DescribeTimeZone(TimeZoneInfo zone) => $"{TimeZoneIds.ToStored(zone.Id)} {zone.DisplayName}";

    private bool IsLocked => board?.IsLocked == true;

    /// <summary>True once somebody has given their update on this date. Locking closes a standup that ran, so a date with no turn on it has nothing to close.</summary>
    private bool HasPresented => board?.Participants.Any(participant => participant.State == AttendanceState.Presented) == true;

    private string LockActionDescription => IsLocked
        ? $"Locked {board?.LockedUtc.ToLocalDisplay(DisplayTimeZone)}. Unlock to record anything further on this date."
        : HasPresented ? "Lock this date so nothing recorded on it can be changed by accident." : "Nobody has presented on this date yet, so there is nothing to lock.";

    private string UpdateHintText => IsLocked ? "Tap the notes icon beside a name to read their update." : "Tap the notes icon beside a name to record their update.";

    protected override async Task OnInitializedAsync()
    {
        standupTimeZone = UiOptions.Value.ResolveTimeZone();
        today = Clock.Today(standupTimeZone);
        standups = await StandupService.GetSelectableAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && await BrowserTimeZone.GetAsync() is { } zone)
        {
            browserTimeZone = zone;
        }

        // The short name depends on the date, for daylight saving, so it is looked up again whenever the shown zone or the meeting date changes.
        if (abbreviatedFor != DisplayTimeZoneKey)
        {
            (string ZoneId, DateTimeOffset Instant) key = DisplayTimeZoneKey;
            displayTimeZoneAbbreviation = await BrowserTimeZone.AbbreviationAsync(DisplayTimeZone, key.Instant);
            abbreviatedFor = key;
            StateHasChanged();
        }
    }

    // The standup and date live in the query string, so the board is reloaded from the URL rather than from component state. That makes any board view shareable.
    protected override async Task OnParametersSetAsync()
    {
        standupPickerValue = SelectedStandupId;

        // Times on the board, and which date is "today", are in the selected standup's own time zone.
        standupTimeZone = UiOptions.Value.ResolveTimeZone(SelectedStandup);
        today = Clock.Today(standupTimeZone);

        if (SelectedStandupId == Guid.Empty || (SelectedStandupId == loadedStandupId && SelectedDate == loadedDate && board is not null))
        {
            return;
        }

        loadedStandupId = SelectedStandupId;
        loadedDate = SelectedDate;

        await LoadBoardAsync();

        // An update left open across a date change stays open only while that person is on the new date's board.
        if (SelectedParticipant is null)
        {
            selectedPersonId = null;
        }
    }

    private async Task LoadBoardAsync()
    {
        IReadOnlyList<DateOnly> week = MeetingCalendar.Week(SelectedDate);
        (board, presentedDates, lockedDates) = (
            await BoardService.GetBoardAsync(SelectedStandupId, SelectedDate),
            await BoardService.GetPresentedDatesAsync(SelectedStandupId, week[0], week[^1]),
            await BoardService.GetLockedDatesAsync(SelectedStandupId, week[0], week[^1]));
    }

    /// <summary>Everyone in one state, in that column's order. The roster lists only the kind of attendee the filter is set to.</summary>
    private IReadOnlyList<BoardParticipantDto> ParticipantsIn(AttendanceState state) => board is null
        ? []
        : board.Participants.Where(participant => participant.State == state && (state != AttendanceState.Roster || InRosterView(participant))).InColumnOrder(state);

    private bool InRosterView(BoardParticipantDto participant) => rosterView is null || participant.Kind == rosterView;

    private string RosterViewName => rosterView is { } kind ? $"the {RosterLabels.RosterTitle(kind.Roster()).ToLowerInvariant()}" : "any roster";

    private string EmptyColumnText(AttendanceState state) => state switch
    {
        AttendanceState.Available => "Tap a name on the roster as you spot them in the meeting.",
        AttendanceState.Presented => "Tap a name again once they have given their update.",
        AttendanceState.Leader => EmptyNonPresenterText(AttendeeKind.Leader),
        AttendanceState.Guest => EmptyNonPresenterText(AttendeeKind.Guest),
        _ when board?.Participants.Any(InRosterView) != true => $"Nobody is on {RosterViewName} for this standup.",
        _ => "Everyone has been marked here.",
    };

    private static string EmptyNonPresenterText(AttendeeKind kind) => $"Switch the roster to {RosterLabels.Plural(kind)} and tap a name as you spot them in the meeting.";

    private async Task OnStandupChangedAsync(Guid standupId)
    {
        standupPickerValue = standupId;

        if (!await NavigateAsync(standupId, SelectedDate))
        {
            standupPickerValue = SelectedStandupId;
        }
    }

    private Task OnDateChangedAsync(DateOnly date) => NavigateAsync(SelectedStandupId, date);

    /// <summary>
    /// Moves the board to another standup or date, once any unsaved edits in the open update have been confirmed as expendable. False when the reader chose to keep editing.
    /// An open update follows a date change, so the same person's update can be read across days, and closes on a standup change.
    /// </summary>
    private async Task<bool> NavigateAsync(Guid standupId, DateOnly date)
    {
        if (!await ConfirmLeaveUpdateAsync())
        {
            return false;
        }

        if (standupId != SelectedStandupId)
        {
            selectedPersonId = null;
        }

        Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [UiRoutes.StandupQueryKey] = standupId,
            [UiRoutes.DateQueryKey] = MeetingCalendar.ToRouteValue(date),
        }));

        return true;
    }

    /// <summary>True when no update is open, or the open one has nothing unsaved or the reader agreed to discard it.</summary>
    private Task<bool> ConfirmLeaveUpdateAsync() => SelectedParticipant is not null && updatePanel is not null
        ? updatePanel.ConfirmDiscardAsync()
        : Task.FromResult(true);

    private async Task OnLeaderChangedAsync(Guid? personId)
    {
        if (board is null)
        {
            return;
        }

        try
        {
            if (!await BoardService.SetLeaderAsync(SelectedStandupId, SelectedDate, personId))
            {
                return;
            }
        }
        catch (BoardLockedException)
        {
            await ReloadLockedBoardAsync();
            return;
        }

        board = board with { LeaderPersonId = personId };
    }

    private async Task OnAnyAttendeeCanLeadChangedAsync(bool anyAttendeeCanLead)
    {
        if (board is null)
        {
            return;
        }

        Guid? leaderPersonId;

        try
        {
            leaderPersonId = await BoardService.SetAnyAttendeeCanLeadAsync(SelectedStandupId, SelectedDate, anyAttendeeCanLead);
        }
        catch (BoardLockedException)
        {
            await ReloadLockedBoardAsync();
            return;
        }

        board = board with { AnyAttendeeCanLead = anyAttendeeCanLead, LeaderPersonId = leaderPersonId };
    }

    /// <summary>Locks the date, or asks before reopening one, since unlocking is what puts a finished standup back within reach of a stray tap.</summary>
    private async Task ToggleLockAsync()
    {
        if (board is null)
        {
            return;
        }

        if (!IsLocked)
        {
            DateTimeOffset? lockedUtc = await BoardService.LockAsync(SelectedStandupId, SelectedDate);
            board = board with { LockedUtc = lockedUtc };
            lockedDates = WithMarker(lockedDates, marked: true);
            selectedPersonId = null;
            Snackbar.Add($"Standup locked as of {lockedUtc.ToLocalDisplay(DisplayTimeZone)}.", Severity.Success);

            return;
        }

        bool? confirmed = await DialogService.ShowMessageBoxAsync(
            "Unlock standup",
            $"Unlock {board.StandupName} for {SelectedDate.ToDateTime(TimeOnly.MinValue).ToString(UiFormats.LongDate)}? Attendance and updates can be changed again.",
            yesText: "Unlock",
            cancelText: "Keep locked");

        if (confirmed != true)
        {
            return;
        }

        await BoardService.UnlockAsync(SelectedStandupId, SelectedDate);
        board = board with { LockedUtc = null };
        lockedDates = WithMarker(lockedDates, marked: false);
        Snackbar.Add("Standup unlocked.", Severity.Success);
    }

    private async Task SelectParticipantAsync(Guid personId)
    {
        if (await ConfirmLeaveUpdateAsync())
        {
            selectedPersonId = selectedPersonId == personId ? null : personId;
        }
    }

    private void CloseUpdate() => selectedPersonId = null;

    /// <summary>Advances one person. The first person marked here on a date brings the reader along from the roster too, since whoever is running the board is at the meeting.</summary>
    private async Task AdvanceAsync(Guid personId)
    {
        bool wasAnyoneAttending = IsAnyoneAttending;

        await AdvanceOneAsync(personId);

        // Someone off today is not at the meeting, so the reader is not brought along when it is their own day off.
        if (!wasAnyoneAttending && IsAnyoneAttending && SelfOnRoster is { } self && self.PersonId != personId && board?.AvailabilityFor(self.PersonId)?.IsOffOnDate != true)
        {
            await AdvanceOneAsync(self.PersonId);
        }
    }

    private Task AdvanceOneAsync(Guid personId) => ApplyAsync(() => BoardService.AdvanceAsync(SelectedStandupId, SelectedDate, personId));

    private bool IsAnyoneAttending => board?.Participants.Any(participant => AttendanceTransitions.IsAttending(participant.State)) == true;

    /// <summary>The reader's own card while it is still on the roster, or null when they are not on this board or are already marked here.</summary>
    private BoardParticipantDto? SelfOnRoster => board?.Participants.FirstOrDefault(participant => participant.State == AttendanceState.Roster && Preferences.IsSelf(participant.Email));

    private Task RevertAsync(Guid personId) => ApplyAsync(() => BoardService.RevertAsync(SelectedStandupId, SelectedDate, personId));

    private Task SaveUpdateAsync(ParticipantUpdateDto update) => ApplyAsync(async () =>
    {
        BoardParticipantDto? saved = await BoardService.SaveUpdateAsync(update);
        Snackbar.Add(saved is { UpdateSavedUtc: null } ? "Update deleted." : "Update saved.", Severity.Success);
        return saved;
    });

    /// <summary>Runs a board mutation and swaps the single changed participant into the loaded board, rather than refetching every roster entry.</summary>
    private async Task ApplyAsync(Func<Task<BoardParticipantDto?>> mutation)
    {
        BoardParticipantDto? updated;

        try
        {
            updated = await mutation();
        }
        catch (BoardLockedException)
        {
            await ReloadLockedBoardAsync();
            return;
        }
        catch (PersonOffException exception)
        {
            Snackbar.Add(exception.Message, Severity.Warning);
            return;
        }

        if (updated is null || board is null)
        {
            return;
        }

        board = board.WithParticipant(updated);
        presentedDates = WithMarker(presentedDates, HasPresented);
    }

    /// <summary>Brings a board that was open when somebody else locked the date back in step, so the screen matches what storage will accept.</summary>
    private async Task ReloadLockedBoardAsync()
    {
        selectedPersonId = null;
        await LoadBoardAsync();
        Snackbar.Add("This standup has been locked, so the board has been reloaded.", Severity.Warning);
    }

    /// <summary>Holds one of the week strip's marker sets in step with the board after a change, so the week does not have to be refetched.</summary>
    private IReadOnlyCollection<DateOnly> WithMarker(IReadOnlyCollection<DateOnly> dates, bool marked) => marked == dates.Contains(SelectedDate)
        ? dates
        : marked ? [.. dates, SelectedDate] : [.. dates.Where(date => date != SelectedDate)];
}
