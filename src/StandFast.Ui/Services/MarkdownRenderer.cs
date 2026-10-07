using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Microsoft.AspNetCore.Components;

namespace StandFast.Ui.Services;

/// <summary>Renders markdown to HTML. One shared, pre-built pipeline: parsing options are a single decision, and building the pipeline per render is wasteful.</summary>
public interface IMarkdownRenderer
{
    /// <param name="interactiveTasks">Renders task list checkboxes enabled, for a view that toggles them through <see cref="ToggleTask"/>.</param>
    MarkupString ToHtml(string? markdown, bool interactiveTasks = false);

    /// <summary>Checks or unchecks the task list item at <paramref name="taskIndex"/>, counted in document order as the rendered checkboxes are.</summary>
    /// <returns>The updated markdown, or <c>null</c> when there is no such task.</returns>
    string? ToggleTask(string? markdown, int taskIndex);
}

public sealed partial class MarkdownRenderer : IMarkdownRenderer
{
    /// <summary>
    /// Advanced extensions give tables, task lists, and auto-links, which is what standup notes actually contain.
    /// <c>DisableHtml</c> is deliberate: update text is user-supplied and rendered into the page, so raw HTML is escaped rather than executed.
    /// </summary>
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Build();

    /// <summary>A paragraph holding only a non-breaking space renders as one empty line; the entity form survives Markdig's whitespace trimming.</summary>
    private const string BlankLineSpacer = "&nbsp;";

    /// <summary>Markdig's task checkbox markup. With raw HTML disabled, no user text can produce it.</summary>
    private const string DisabledTaskCheckbox = "<input disabled=\"disabled\" type=\"checkbox\"";
    private const string EnabledTaskCheckbox = "<input type=\"checkbox\"";

    private const char UncheckedTaskMark = ' ';
    private const char CheckedTaskMark = 'x';

    private const char LineFeed = '\n';
    private const int MinFenceLength = 3;
    private static readonly char[] FenceChars = ['`', '~'];

    public MarkupString ToHtml(string? markdown, bool interactiveTasks = false)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarkupString(string.Empty);
        }

        var html = Markdown.ToHtml(PreserveBlankLines(markdown), Pipeline);
        return new MarkupString(interactiveTasks ? html.Replace(DisabledTaskCheckbox, EnabledTaskCheckbox, StringComparison.Ordinal) : html);
    }

    public string? ToggleTask(string? markdown, int taskIndex)
    {
        if (string.IsNullOrEmpty(markdown) || taskIndex < 0)
        {
            return null;
        }

        var task = Markdown.Parse(markdown, Pipeline).Descendants<TaskList>().ElementAtOrDefault(taskIndex);
        if (task is null)
        {
            return null;
        }

        // The span starts at the opening bracket, so the mark sits one character in.
        var markPosition = task.Span.Start + 1;
        var chars = markdown.ToCharArray();
        chars[markPosition] = task.Checked ? UncheckedTaskMark : CheckedTaskMark;
        return new string(chars);
    }

    /// <summary>
    /// Turns every blank line between content into its own spacer paragraph, so the preview shows the same vertical spacing as the editor.
    /// Markdown otherwise collapses any run of blank lines into one zero-margin paragraph break. Lines inside fenced code blocks are left as typed.
    /// A blank line inside a list stays blank when the list carries on after it, as a nested line or another item of the same kind, since a spacer would split the list.
    /// </summary>
    private static string PreserveBlankLines(string markdown)
    {
        var output = new StringBuilder(markdown.Length);
        var hasContent = false;
        string? listKind = null;
        string? openFence = null;
        var lines = markdown.ReplaceLineEndings(LineFeed.ToString()).TrimEnd().Split(LineFeed);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();

            if (openFence is null && trimmed.Length == 0)
            {
                if (hasContent)
                {
                    output.Append(LineFeed);
                    if (listKind is null || !ListContinuesAfter(lines, index, listKind))
                    {
                        output.Append(BlankLineSpacer).Append(LineFeed).Append(LineFeed);
                    }
                }

                continue;
            }

            if (openFence is null && !char.IsWhiteSpace(line[0]))
            {
                listKind = ListKind(line);
            }

            if (openFence is null)
            {
                openFence = FenceMarker(trimmed);
            }
            else if (trimmed.StartsWith(openFence, StringComparison.Ordinal) && trimmed.TrimEnd(openFence[0]).Length == 0)
            {
                openFence = null;
            }

            hasContent = true;
            output.Append(line).Append(LineFeed);
        }

        return output.ToString();
    }

    private static bool ListContinuesAfter(string[] lines, int blankIndex, string listKind)
    {
        var next = lines.Skip(blankIndex + 1).FirstOrDefault(line => line.Trim().Length > 0);
        return next is not null && (char.IsWhiteSpace(next[0]) || ListKind(next) == listKind);
    }

    /// <summary>What makes consecutive top-level items one list: the bullet character, or the delimiter after a number. <c>null</c> when the line is not a list item.</summary>
    private static string? ListKind(string line)
    {
        var match = ListItemMarker().Match(line);
        return match.Success ? match.Groups["kind"].Value : null;
    }

    /// <summary>A bullet (<c>-</c>, <c>*</c>, <c>+</c>) or numbered (<c>1.</c>, <c>1)</c>) list item marker at the start of a line.</summary>
    [GeneratedRegex(@"^(?:(?<kind>[-*+])|\d{1,9}(?<kind>[.)]))(?:\s|$)")]
    private static partial Regex ListItemMarker();

    /// <summary>The opening run of a code fence (three or more backticks or tildes), or <c>null</c> when the line does not open one.</summary>
    private static string? FenceMarker(string trimmedLine)
    {
        if (trimmedLine.Length < MinFenceLength || Array.IndexOf(FenceChars, trimmedLine[0]) < 0)
        {
            return null;
        }

        var length = trimmedLine.Length - trimmedLine.TrimStart(trimmedLine[0]).Length;
        return length >= MinFenceLength ? new string(trimmedLine[0], length) : null;
    }
}
