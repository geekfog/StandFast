using System.Text;
using Markdig;
using Microsoft.AspNetCore.Components;

namespace StandFast.Ui.Services;

/// <summary>Renders markdown to HTML. One shared, pre-built pipeline: parsing options are a single decision, and building the pipeline per render is wasteful.</summary>
public interface IMarkdownRenderer
{
    MarkupString ToHtml(string? markdown);
}

public sealed class MarkdownRenderer : IMarkdownRenderer
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

    private const char LineFeed = '\n';
    private const int MinFenceLength = 3;
    private static readonly char[] FenceChars = ['`', '~'];

    public MarkupString ToHtml(string? markdown) =>
        string.IsNullOrWhiteSpace(markdown) ? new MarkupString(string.Empty) : new MarkupString(Markdown.ToHtml(PreserveBlankLines(markdown), Pipeline));

    /// <summary>
    /// Turns every blank line between content into its own spacer paragraph, so the preview shows the same vertical spacing as the editor.
    /// Markdown otherwise collapses any run of blank lines into one zero-margin paragraph break. Lines inside fenced code blocks are left as typed.
    /// </summary>
    private static string PreserveBlankLines(string markdown)
    {
        var output = new StringBuilder(markdown.Length);
        var hasContent = false;
        string? openFence = null;

        foreach (var line in markdown.ReplaceLineEndings(LineFeed.ToString()).TrimEnd().Split(LineFeed))
        {
            var trimmed = line.Trim();

            if (openFence is null && trimmed.Length == 0)
            {
                if (hasContent)
                {
                    output.Append(LineFeed).Append(BlankLineSpacer).Append(LineFeed).Append(LineFeed);
                }

                continue;
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
