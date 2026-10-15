namespace StandFast.Ui.Common;

/// <summary>One series' drawing style: the stroke colour and, for every colour beyond the first pass, the dash pattern that tells it apart.</summary>
/// <param name="DashArray">SVG <c>stroke-dasharray</c> value, or null for an unbroken line.</param>
/// <param name="PatternName">Spoken name of the dash pattern, used in the legend's accessible text so identity never rests on colour alone.</param>
public sealed record SeriesStyle(string Color, string? DashArray, string PatternName);

/// <summary>
/// Assigns a distinct line style to each series of a chart. Colours come from a palette validated for colour-vision deficiency separation and
/// for contrast in both themes; once every colour has been used, the palette repeats under the next dash pattern, so a repeated colour is
/// always paired with a line the eye can tell apart.
/// </summary>
public static class SeriesStyles
{
    /// <summary>
    /// The categorical hues, in the order they are handed out: blue, orange, teal, magenta, yellow, violet. Each carries a light-theme and a
    /// dark-theme step of the same hue, so a series keeps its identity when the theme is switched. Lines cross every other line, so every pair
    /// is kept at least 15 apart (OKLab ΔE×100) under normal, protan, and deutan vision, with each step at 3:1 or better against its theme's
    /// surface. Six hues is the most that clears that floor in both themes; a seventh hue always lands too close to one already here.
    /// </summary>
    private static readonly (string Light, string Dark)[] Hues =
    [
        ("#3a90fb", "#79aff7"),
        ("#784104", "#df7d03"),
        ("#30a4a0", "#7fe2ca"),
        ("#a41884", "#b76d8a"),
        ("#9c7907", "#fdc234"),
        ("#6216e1", "#8f63fe"),
    ];

    /// <summary>
    /// Dash patterns in the order the palette is repeated under them, solid first. Each is a visibly different rhythm at the 2px stroke the
    /// charts draw, which is what lets a colour be reused without the two lines reading as one.
    /// </summary>
    private static readonly (string Name, string? DashArray)[] Patterns =
    [
        ("Solid", null),
        ("Small dash", "3 3"),
        ("Large dash", "10 4"),
        ("Dash-dot", "10 4 2 4"),
        ("Dotted", "1 4"),
        ("Dash-dot-dot", "10 4 2 4 2 4"),
        ("Medium dash", "6 4"),
        ("Long dash", "18 5"),
        ("Long dash, short dash", "18 5 5 5"),
        ("Dot-dash", "2 4 8 4"),
        ("Small dash, dotted", "4 3 1 3"),
        ("Long dash, dotted", "16 4 1 4"),
        ("Extra long dash", "26 6"),
    ];

    /// <summary>How many series can be drawn before a style is used a second time: every hue under every pattern.</summary>
    public static int Count => Hues.Length * Patterns.Length;

    /// <summary>
    /// The style for the series at <paramref name="seriesIndex"/>. Indexes fill every hue at one pattern before moving to the next, so a chart
    /// with few series is drawn entirely in solid lines.
    /// </summary>
    public static SeriesStyle For(int seriesIndex, bool isDarkMode)
    {
        int slot = ((seriesIndex % Count) + Count) % Count;
        (string name, string? dashArray) = Patterns[slot / Hues.Length];
        (string light, string dark) = Hues[slot % Hues.Length];

        return new SeriesStyle(isDarkMode ? dark : light, dashArray, name);
    }
}
