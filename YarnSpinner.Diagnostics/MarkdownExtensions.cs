using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace YarnSpinner.Diagnostics;

/// <summary>
/// Utility methods for working with Markdown and YAML frontmatter
/// </summary>
public static class MarkdownExtensions
{
    private static readonly IDeserializer YamlDeserializer =
        new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly MarkdownPipeline Pipeline
        = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .Build();

    private static readonly Regex EscapedCharactersRegex = new(@"[\n\r""]");
    private static readonly Regex NewlinesRegex = new(@"[\n\r]");
    private static readonly Regex MultipleSpacesRegex = new(@"\s{2,}");

    /// <summary>
    /// Escapes a string by adding a backslash \ in front of characters that
    /// can't appear inside a string literal in C#
    /// </summary>
    /// <param name="str">The string to escape</param>
    /// <returns>The escaped string</returns>
    public static string Escape(this string str)
    {
        return EscapedCharactersRegex.Replace(str, d => "\\" + d.Value);
    }

    /// <summary>
    /// Unwraps a string into a single line by replacing all newlines with
    /// spaces, and collapsing multiple spaces into a single space, and then
    /// trimming it.
    /// </summary>
    /// <param name="str">The string to unwrap.</param>
    /// <returns>The unwrapped string.</returns>
    public static string UnwrapAndTrim(this string str)
    {

        var working = NewlinesRegex.Replace(str, " ");
        working = MultipleSpacesRegex.Replace(working, " ");
        return working.Trim();
    }

    /// <summary>
    /// Replaces XML-unsafe characters (&lt; and &gt;) with HTML entities.
    /// </summary>
    /// <param name="str">The string to escape.</param>
    /// <returns>The escaped string.</returns>
    public static string EscapeXML(this string str)
    {
        return str.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>
    /// Parses a document containing markdown to get its front matter, and
    /// returns the parsed result and the remaining markdown.
    /// </summary>
    /// <typeparam name="T">The type of frontmatter to parse.</typeparam>
    /// <param name="markdown">The markdown to parse.</param>
    /// <returns>A tuple containing the parsed frontmatter and the remaining
    /// markdown text.</returns>
    /// <exception cref="YamlDotNet.Core.YamlException">Thrown when there is an
    /// exception encountered when parsing the frontmatter.</exception>
    public static (T?, string) GetFrontMatter<T>(this string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var block = document
            .Descendants<YamlFrontMatterBlock>()
            .FirstOrDefault();

        var writer = new StringWriter();
        var renderer = new Markdig.Renderers.Normalize.NormalizeRenderer(writer);
        Pipeline.Setup(renderer);

        if (block == null)
        {
            renderer.Render(document);
            // No frontmatter, so no YAML
            return (default, writer.ToString());
        }

        // Extract the YAML from the frontmatter...
        var yaml =
            block
            // this is not a mistake
            // we have to call .Lines 2x
            .Lines // StringLineGroup[]
            .Lines // StringLine[]
            .OrderByDescending(x => x.Line)
            .Select(x => $"{x}\n")
            .ToList()
            .Select(x => x.Replace("---", string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Aggregate((s, agg) => agg + s);

        // ...and deserialize it
        try
        {

            var frontmatter = YamlDeserializer.Deserialize<T>(yaml);

            // Remove the frontmatter from the markdown
            block.Remove();

            renderer.Render(document);

            // Finally, return our frontmatter alongside the normalised markdown
            return (frontmatter, writer.ToString());
        }
        catch (YamlDotNet.Core.YamlException e)
        {
            throw new YamlDotNet.Core.YamlException($"Exception when parsing markdown: line {e.Start.Line}: {e.Message}", e);
        }
    }
}
