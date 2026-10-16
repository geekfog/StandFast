using MudBlazor;

namespace StandFast.Ui.Common;

/// <summary>The formatted editor's handler for a toolbar command. markdown-visual.js keys its actions by these names.</summary>
public enum MarkdownVisualAction
{
    Bold,
    Italic,
    Strikethrough,
    InlineCode,
    Heading,
    BulletedList,
    NumberedList,
    TaskList,
    Outdent,
    Indent,
    Quote,
    Link,
}

/// <summary>One toolbar action. <see cref="LinePrefix"/> distinguishes block commands (lists, quotes, headings) from inline wrapping commands in the markdown source.</summary>
/// <param name="Tooltip">Button tooltip, also used as the accessible label.</param>
/// <param name="Icon">MudBlazor icon path.</param>
/// <param name="VisualAction">What the button does in the formatted editor, which works on the formatted text rather than the markdown source.</param>
/// <param name="Before">Text inserted before the selection, for inline commands.</param>
/// <param name="After">Text inserted after the selection, for inline commands.</param>
/// <param name="Placeholder">Inserted when nothing is selected, so the button always produces valid markdown.</param>
/// <param name="LinePrefix">Prefix applied to each selected line, for block commands. The formatted editor takes a heading's level from it.</param>
/// <param name="Ordered">True for a numbered list, which renumbers rather than repeating a fixed prefix.</param>
/// <param name="ListShift">1 indents the selected list items one level and -1 outdents them; 0 for every other command.</param>
public sealed record MarkdownCommand(string Tooltip, string Icon, MarkdownVisualAction VisualAction, string Before = "", string After = "", string Placeholder = "", string? LinePrefix = null, bool Ordered = false, int ListShift = 0)
{
    public bool IsBlockCommand => LinePrefix is not null || Ordered;

    public bool IsListShiftCommand => ListShift != 0;
}

/// <summary>The markdown toolbar definition, shared by every markdown field so all three board boxes offer identical formatting.</summary>
public static class MarkdownCommands
{
    public const string ItalicMarker = "_";

    /// <summary>Separates two markdown blocks, so a heading line and the list or paragraph after it render as separate blocks.</summary>
    public const string BlockSeparator = "\n\n";

    public static readonly IReadOnlyList<MarkdownCommand> All =
    [
        new("Bold", Icons.Material.Filled.FormatBold, MarkdownVisualAction.Bold, "**", "**", "bold text"),
        new("Italic", Icons.Material.Filled.FormatItalic, MarkdownVisualAction.Italic, ItalicMarker, ItalicMarker, "italic text"),
        new("Strikethrough", Icons.Material.Filled.FormatStrikethrough, MarkdownVisualAction.Strikethrough, "~~", "~~", "struck through"),
        new("Inline code", Icons.Material.Filled.Code, MarkdownVisualAction.InlineCode, "`", "`", "code"),
        new("Heading", Icons.Material.Filled.Title, MarkdownVisualAction.Heading, LinePrefix: "## "),
        new("Bulleted list", Icons.Material.Filled.FormatListBulleted, MarkdownVisualAction.BulletedList, LinePrefix: "- "),
        new("Numbered list", Icons.Material.Filled.FormatListNumbered, MarkdownVisualAction.NumberedList, Ordered: true),
        new("Task list", Icons.Material.Filled.CheckBox, MarkdownVisualAction.TaskList, LinePrefix: "- [ ] "),
        new("Outdent list item", Icons.Material.Filled.FormatIndentDecrease, MarkdownVisualAction.Outdent, ListShift: -1),
        new("Indent list item", Icons.Material.Filled.FormatIndentIncrease, MarkdownVisualAction.Indent, ListShift: 1),
        new("Quote", Icons.Material.Filled.FormatQuote, MarkdownVisualAction.Quote, LinePrefix: "> "),
        new("Link", Icons.Material.Filled.Link, MarkdownVisualAction.Link, "[", "](https://)", "link text"),
    ];
}
