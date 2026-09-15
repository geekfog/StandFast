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

    public MarkupString ToHtml(string? markdown) =>
        string.IsNullOrWhiteSpace(markdown) ? new MarkupString(string.Empty) : new MarkupString(Markdown.ToHtml(markdown, Pipeline));
}
