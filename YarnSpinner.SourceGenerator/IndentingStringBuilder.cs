// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Analyser;

#nullable enable

using System;
using System.Text;


/// <summary>
/// A tweak upon the idea of a stringbuilder, but this one understands how indented the current line of text should be.
/// Works as a wrapper around a normal string builder.
/// </summary>
/// <remarks>
/// Based on discussion around https://github.com/dotnet/roslyn/issues/71162
/// the one there is a lot more advanced but doesn't seem to work out of the box
/// and rather than spend ages trying to fix it gonna just hack together my own quickly using that one as inspiration
/// </remarks>
/// <param name="stringBuilder"></param>
/// <param name="indentLevel"></param>
internal class IndentingStringBuilder(StringBuilder stringBuilder, int indentLevel = 0)
{
    private const int indentSize = 4;
    private const int maxIndentationLevel = 8;
    private static ReadOnlySpan<char> endOfLineCharacters => ['\r', '\n', '\f', '\u0085', '\u2028', '\u2029'];
    private static string[] splitPoints => ["\r\n", "\r", "\n"];

    public IndentingStringBuilder(int indentLevel = 0) : this(new StringBuilder(), indentLevel) { }

    /// <summary>
    /// The current indentation level of the string builder
    /// </summary>
    public int IndentationLevel { get; private set; } = indentLevel;

    private string Indentation => new(' ', IndentationLevel * indentSize);

    /// <summary>
    /// Increases the current indentation level up to the global max
    /// </summary>
    public void Indent() => IndentationLevel = Math.Min(IndentationLevel + 1, maxIndentationLevel);
    /// <summary>
    /// Decreases current indentation level down to a min of 0
    /// </summary>
    public void Dedent() => IndentationLevel = Math.Max(0, IndentationLevel - 1);

    /// <summary>
    /// Writes a single empty new line
    /// </summary>
    public void AppendLine()
    {
        stringBuilder.AppendLine();
    }
    
    /// <summary>
    /// Writes a line at the current indentation level
    /// </summary>
    /// <param name="text">the text to write to the builder</param>
    /// <param name="containsLineBreaks">If true will split the <see cref="text"/> up by newlines and remerge them in at the appropriate level of indentation</param>
    public void AppendLine(string text, bool containsLineBreaks = true)
    {
        if (!containsLineBreaks)
        {
            stringBuilder.AppendLine(Indentation + text);
        }
        else
        {
            // I wonder if there is a better way of doing this
            var lines = text.Split(splitPoints, StringSplitOptions.None);
            foreach (var line in lines)
            {
                stringBuilder.AppendLine(Indentation + line);
            }
        }
    }
    /// <summary>
    /// Writes the text into the string builder but doesn't add a new line.
    /// </summary>
    /// <remarks>
    /// Correctly (I think) handles only adding the new line to the first append call each line to prevent having weird internal spacing
    /// </remarks>
    /// <param name="text">Text to be added</param>
    public void Append(string text)
    {
        // if this append is the first element after a new line (or the first element) then we add the indentation
        // otherwise we don't
        // this way if we have a series of append calls we only add the extra spaces to the first one
        if (stringBuilder.Length == 0 || IsEndOfLineCharacter(stringBuilder[stringBuilder.Length -1]))
        {
            stringBuilder.Append(Indentation + text);
        }
        else
        {
            stringBuilder.Append(text);
        }
    }

    private static bool IsEndOfLineCharacter(char ch) => endOfLineCharacters.IndexOf(ch) >= 0;

    /// <summary>
    /// Appends a new line as a format string
    /// </summary>
    /// <param name="format">the format string</param>
    /// <param name="elements">the elements to add to the format</param>
    public void AppendFormat(string format, params object[] elements)
    {
        AppendLine(string.Format(format, elements), false);
    }
    /// <summary>
    /// A multiline version of the <see cref="AppendFormat"/> method.
    /// </summary>
    /// <param name="format">the format string</param>
    /// <param name="elements">the elements to add to the format</param>
    public void AppendMultilineFormat(string format, params object[] elements)
    {
        AppendLine(string.Format(format, elements), true);
    }

    public override string ToString() => stringBuilder.ToString();

    /// <summary>
    /// Convenience structure to allow easily defining indented regions
    /// </summary>
    public readonly struct Region(IndentingStringBuilder builder, string close) : IDisposable
    {
        public readonly void Dispose()
        {
            builder.Dedent();
            builder.AppendLine(close);
        }
    }
    /// <summary>
    /// Creates a standard squiggly bracket delimited and indented region
    /// </summary>
    public Region EnterBlock() => EnterIndentedRegion("{", "}");
    
    /// <summary>
    /// Creates a new squiggly bracket delimited and indented region but with a single line above it.
    /// This is good for creating the very common if statement
    /// </summary>
    /// <param name="blockOpener">the line of text to go above the indented region</param>
    public Region EnterBlock(string blockOpener)
    {
        this.AppendLine(blockOpener);
        return EnterIndentedRegion("{", "}");
    }

    /// <summary>
    /// Creates an indented region but without the squiggly brackets
    /// </summary>
    public Region EnterIndentedRegion() => EnterIndentedRegion("", "");

    private Region EnterIndentedRegion(string open, string close)
    {
        this.AppendLine(open);
        this.Indent();
        return new Region(this, close);
    }
}