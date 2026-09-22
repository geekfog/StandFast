using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StandFast.Application.Dtos;
using StandFast.Application.Reporting;
using StandFast.Application.Services;
using StandFast.Ui.Common;
using StandFast.Ui.Configuration;

namespace StandFast.Ui.Components.Pages;

public partial class Reports
{
    private IReadOnlyList<StandupDto> standups = [];
    private PresentingOrderTimelineDto? timeline;
    private TimeZoneInfo timeZone = TimeZoneInfo.Local;
    private Guid loadedStandupId;
    private int loadedDays;

    /// <summary>The signed-in user's settings. The chart takes its theme from these, because series colours are chosen in code rather than by a stylesheet.</summary>
    [CascadingParameter]
    private UserPreferencesDto Preferences { get; set; } = UserPreferencesDto.Default;

    [Inject]
    private IStandupService StandupService { get; set; } = default!;

    [Inject]
    private IReportService ReportService { get; set; } = default!;

    [Inject]
    private IOptions<StandFastUiOptions> UiOptions { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = UiRoutes.ReportQueryKey)]
    private string? ReportQuery { get; set; }

    [SupplyParameterFromQuery(Name = UiRoutes.StandupQueryKey)]
    private Guid? StandupIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = UiRoutes.DaysQueryKey)]
    private int? DaysQuery { get; set; }

    private ReportKind SelectedReport => Enum.TryParse(ReportQuery, ignoreCase: true, out ReportKind parsed) && ReportCatalog.All.Contains(parsed)
        ? parsed
        : ReportCatalog.Default;

    private Guid SelectedStandupId => StandupIdQuery ?? standups.FirstOrDefault()?.Id ?? Guid.Empty;

    private int SelectedDays => ReportWindow.ResolveDays(DaysQuery);

    protected override async Task OnInitializedAsync()
    {
        timeZone = UiOptions.Value.ResolveTimeZone();
        standups = await StandupService.GetSelectableAsync();
    }

    // The whole selection lives in the query string, so a report is reloaded from the URL rather than from component state and any view is shareable.
    protected override async Task OnParametersSetAsync()
    {
        if (SelectedStandupId == Guid.Empty || (SelectedStandupId == loadedStandupId && SelectedDays == loadedDays && timeline is not null))
        {
            return;
        }

        loadedStandupId = SelectedStandupId;
        loadedDays = SelectedDays;
        timeline = await ReportService.GetPresentingOrderTimelineAsync(SelectedStandupId, SelectedDays, timeZone);
    }

    private Task OnReportChangedAsync(ReportKind report) => NavigateAsync(report, SelectedStandupId, SelectedDays);

    private Task OnStandupChangedAsync(Guid standupId) => NavigateAsync(SelectedReport, standupId, SelectedDays);

    private Task OnDaysChangedAsync(int days) => NavigateAsync(SelectedReport, SelectedStandupId, days);

    private Task NavigateAsync(ReportKind report, Guid standupId, int days)
    {
        Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [UiRoutes.ReportQueryKey] = report.ToString(),
            [UiRoutes.StandupQueryKey] = standupId,
            [UiRoutes.DaysQueryKey] = days,
        }));

        return Task.CompletedTask;
    }
}
