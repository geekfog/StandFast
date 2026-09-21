using MudBlazor;

namespace StandFast.Ui.Common;

/// <summary>The app's MudBlazor theme. Board column colours are derived from these palette entries so the UI stays consistent if the palette changes.</summary>
public static class StandFastTheme
{
    // Tertiary is the red that marks a participant with an update recorded, in each theme. It carries the primary green's saturation and lightness at
    // hue 355, so it sits at the same visual weight against its background. The exact complement of the green is a plum, which does not read as red.
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2f6f4e",
            Secondary = "#4a6fa5",
            Tertiary = "#6f2f34",
            AppbarBackground = "#2f6f4e",
            Background = "#f6f7f5",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#6fbf8f",
            Secondary = "#7fa3d6",
            Tertiary = "#bf6f76",
            AppbarBackground = "#1d2a23",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
        },
    };

    /// <summary>Colour assigned to each board column, used by both the column header and the participant chips.</summary>
    public static Color ColumnColor(Domain.Enums.AttendanceState state) => state switch
    {
        Domain.Enums.AttendanceState.Available => Color.Info,
        Domain.Enums.AttendanceState.Presented => Color.Success,
        _ => Color.Default,
    };

    /// <summary>Column heading shown above each board list.</summary>
    public static string ColumnTitle(Domain.Enums.AttendanceState state) => state switch
    {
        Domain.Enums.AttendanceState.Available => "Present, can be called on",
        Domain.Enums.AttendanceState.Presented => "Presented",
        _ => "Roster",
    };

    /// <summary>Icon shown on each board column header.</summary>
    public static string ColumnIcon(Domain.Enums.AttendanceState state) => state switch
    {
        Domain.Enums.AttendanceState.Available => Icons.Material.Filled.HowToReg,
        Domain.Enums.AttendanceState.Presented => Icons.Material.Filled.CheckCircle,
        _ => Icons.Material.Filled.Group,
    };
}
