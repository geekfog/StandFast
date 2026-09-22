using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StandFast.Application.Dtos;
using StandFast.Domain.Common;

namespace StandFast.Ui.Components.Reports;

public partial class PresentingOrderTimelineChart : IAsyncDisposable
{
    /// <summary>Gap between an axis label and the plot it labels.</summary>
    private const int AxisLabelGap = 8;

    /// <summary>Radius of a presented-day marker. Diameter stays at or above the 8px a pointer can comfortably hit.</summary>
    private const int DotRadius = 4;

    private const int LegendSwatchWidth = 36;
    private const int LegendSwatchHeight = 10;

    private const string ObserveFunction = "standFastChart.observe";
    private const string DisconnectFunction = "standFastChart.disconnect";

    /// <summary>Width changes smaller than this are ignored, so a scrollbar appearing or a rounding difference cannot start a render loop.</summary>
    private const int WidthChangeThreshold = 2;

    private readonly string hostId = $"standfast-chart-{Guid.CreateVersion7():N}";
    private DotNetObjectReference<PresentingOrderTimelineChart>? selfReference;
    private int availableWidth;

    [Parameter]
    [EditorRequired]
    public PresentingOrderTimelineDto Report { get; set; } = default!;

    /// <summary>Which of each hue's two steps to draw with. Series colours are chosen in code because the theme is a component state rather than a media query.</summary>
    [Parameter]
    public bool IsDarkMode { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>Held rather than recomputed, because the markup reads positions from it once per grid line, axis label, and marker.</summary>
    private ChartGeometry Geometry { get; set; } = default!;

    protected override void OnParametersSet() => Geometry = new ChartGeometry(Report, availableWidth);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        selfReference = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync(ObserveFunction, hostId, selfReference);
    }

    /// <summary>Called by the browser with the room the chart has, on the first paint and on every resize after it.</summary>
    [JSInvokable]
    public Task OnWidthChangedAsync(int width)
    {
        if (Math.Abs(width - availableWidth) < WidthChangeThreshold)
        {
            return Task.CompletedTask;
        }

        availableWidth = width;
        Geometry = new ChartGeometry(Report, availableWidth);

        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync(DisconnectFunction, hostId);
        }
        catch (JSDisconnectedException)
        {
            // The circuit closed before the component did, which takes the observer with it.
        }

        selfReference?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Turns the report into pixel positions. The order axis runs downward so the first turn sits at the top, and carries one extra row beneath the
/// highest turn for the days a person did not present. The dates are spread across whatever width the browser reports, down to a floor that keeps
/// their labels apart; past that floor the plot grows wider than the window and scrolls.
/// </summary>
internal sealed class ChartGeometry
{
    /// <summary>Left edge of the plot. The space to its left holds the order labels.</summary>
    public const int PlotLeft = 52;

    public const int PlotTop = 16;

    /// <summary>Closest two dates are allowed to sit. Below this their rotated labels start to overlap.</summary>
    private const int MinimumColumnStep = 34;

    private const int RowStep = 26;

    /// <summary>Space inside the plot before the first date and after the last, so an end marker is not clipped by the frame.</summary>
    private const int ColumnPadding = 20;

    private const int RightPadding = 16;

    /// <summary>Room beneath the plot for the date labels, which are rotated to stay legible however tightly the dates are packed.</summary>
    private const int DateLabelHeight = 72;

    private readonly PresentingOrderTimelineDto report;
    private readonly int columnStep;

    /// <param name="availableWidth">What the browser has reported the chart may occupy. Zero before the first measurement, which lays the dates out at the floor.</param>
    public ChartGeometry(PresentingOrderTimelineDto report, int availableWidth)
    {
        this.report = report;

        int spacedWidth = availableWidth - PlotLeft - (ColumnPadding * 2) - RightPadding;
        columnStep = Math.Max(MinimumColumnStep, spacedWidth / Gaps(report));
    }

    /// <summary>The row a person drops to on a day they did not present, one below the highest turn anyone took.</summary>
    private int NoTurnRow => report.HighestOrder + 1;

    /// <summary>Every order row top to bottom, ending with the row that stands for "did not present".</summary>
    public IEnumerable<int> Rows => Enumerable.Range(PresentationOrder.FirstTurn, NoTurnRow);

    public int PlotRight => PlotLeft + (ColumnPadding * 2) + (Math.Max(report.MeetingDates.Count - 1, 0) * columnStep);

    public int PlotBottom => RowY(NoTurnRow);

    public int Width => PlotRight + RightPadding;

    public int Height => PlotBottom + DateLabelHeight;

    public int ColumnX(int column) => PlotLeft + ColumnPadding + (column * columnStep);

    public int RowY(int row) => PlotTop + ((row - PresentationOrder.FirstTurn) * RowStep);

    /// <summary>The label on an order row: its turn number, or the symbol standing in for a day the person did not present.</summary>
    public string RowLabel(int row) => row == NoTurnRow ? PresentationOrder.NoTurnSymbol : row.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// The presenter's line across every meeting date. It runs unbroken over the whole period, dropping to the "did not present" row on the
    /// days they were not called, so a gap in someone's attendance reads as a shape rather than as a missing line.
    /// </summary>
    public string LinePoints(PresenterTimelineDto presenter) => string.Join(' ', presenter.OrdersByDate
        .Select((order, column) => string.Create(CultureInfo.InvariantCulture, $"{ColumnX(column)},{RowY(order ?? NoTurnRow)}")));

    /// <summary>Spaces between dates, which is what the available width is divided across. A report of one date has no gap and takes the floor.</summary>
    private static int Gaps(PresentingOrderTimelineDto report) => Math.Max(report.MeetingDates.Count - 1, 1);
}
