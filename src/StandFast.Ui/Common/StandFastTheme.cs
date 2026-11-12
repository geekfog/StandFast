using MudBlazor;
using MudBlazor.Utilities;

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

    /// <summary>The app bar's color as a hex string, used to tint the browser and installed-app title bar to match it.</summary>
    public static string AppBarColor(bool isDarkMode) =>
        (isDarkMode ? Default.PaletteDark.AppbarBackground : Default.PaletteLight.AppbarBackground).ToString(MudColorOutputFormats.Hex);

    /// <summary>Colour assigned to each board column, used by both the column header and the participant chips.</summary>
    public static Color ColumnColor(Domain.Enums.AttendanceState state) => state switch
    {
        Domain.Enums.AttendanceState.Available => Color.Info,
        Domain.Enums.AttendanceState.Presented => Color.Success,
        Domain.Enums.AttendanceState.Leader => Color.Tertiary,
        Domain.Enums.AttendanceState.Guest => Color.Secondary,
        _ => Color.Default,
    };

    /// <summary><see cref="ColumnColor"/> as a CSS value that follows the light or dark palette, for styling that a MudBlazor <see cref="Color"/> parameter cannot reach.</summary>
    public static string ColumnCssColor(Domain.Enums.AttendanceState state) => ColumnColor(state) switch
    {
        Color.Default => "var(--mud-palette-text-secondary)",
        Color color => $"var(--mud-palette-{color.ToString().ToLowerInvariant()})",
    };

    /// <summary>Column heading shown above each board list.</summary>
    public static string ColumnTitle(Domain.Enums.AttendanceState state) => state switch
    {
        Domain.Enums.AttendanceState.Available => "Present, can be called on",
        Domain.Enums.AttendanceState.Presented => "Presented",
        Domain.Enums.AttendanceState.Leader => "Leaders, not presenting",
        Domain.Enums.AttendanceState.Guest => "Guests, not presenting",
        _ => "Roster",
    };

    /// <summary>Icon for a date's lock state, so the board's button and the activity report mark a closed day the same way.</summary>
    public static string LockIcon(bool isLocked) => isLocked ? Icons.Material.Filled.Lock : Icons.Material.Filled.LockOpen;

    /// <summary>Colour that goes with <see cref="LockIcon"/>. Orange marks a closed day wherever one appears, including the week strip's marker.</summary>
    public static Color LockColor(bool isLocked) => isLocked ? Color.Warning : Color.Default;

    /// <summary>Wording for a date's lock state, used on its own and inside the labels that describe the lock action.</summary>
    public static string LockTitle(bool isLocked) => isLocked ? "Locked" : "Open";

    /// <summary>Icon for opening a person's update, shared by each card's button and the board's hint that points at it.</summary>
    public const string UpdateIcon = Icons.Material.Filled.EditNote;

    /// <summary>Icon shown on each board column header.</summary>
    public static string ColumnIcon(Domain.Enums.AttendanceState state) => state switch
    {
        Domain.Enums.AttendanceState.Available => Icons.Material.Filled.HowToReg,
        Domain.Enums.AttendanceState.Presented => Icons.Material.Filled.CheckCircle,
        Domain.Enums.AttendanceState.Leader => RosterLabels.RosterIcon(Domain.Enums.RosterRole.Leader),
        Domain.Enums.AttendanceState.Guest => Icons.Material.Filled.EmojiPeople,
        _ => Icons.Material.Filled.Group,
    };

    /// <summary>Icon marking how someone takes part, matching the header of the section a tap from the roster places them in.</summary>
    public static string KindIcon(Domain.Enums.AttendeeKind kind) => ColumnIcon(Domain.Common.AttendanceTransitions.Attending(kind));

    /// <summary>Colour that goes with <see cref="KindIcon"/>, matching the same section's header.</summary>
    public static Color KindColor(Domain.Enums.AttendeeKind kind) => ColumnColor(Domain.Common.AttendanceTransitions.Attending(kind));
}
