namespace Yarn.Analyser;

#nullable enable

using System;
using System.Text;

// based on discussion around https://github.com/dotnet/roslyn/issues/71162
// the one there is a lot more advanced but doesn't seem to work out of the box
// and rather than spend ages trying to fix it gonna just hack together my own quickly using that one as inspiration

internal class IndentingStringBuilder(StringBuilder stringBuilder, int indentLevel = 0)
{
    private const int indentSize = 4;
    private const int maxIndentationLevel = 8;
    private static ReadOnlySpan<char> endOfLineCharacters => ['\r', '\n', '\f', '\u0085', '\u2028', '\u2029'];
    private static string[] splitPoints => ["\r\n", "\r", "\n"];

    public IndentingStringBuilder(int indentLevel = 0) : this(new StringBuilder(), indentLevel) { }

    public int IndentationLevel { get; private set; } = indentLevel;

    public string Indentation => new(' ', IndentationLevel * indentSize);

    public void Indent() => IndentationLevel = Math.Min(IndentationLevel + 1, maxIndentationLevel);
    public void Dedent() => IndentationLevel = Math.Max(0, IndentationLevel - 1);

    public void AppendLine()
    {
        stringBuilder.AppendLine();
    }
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

    public void AppendFormat(string format, params object[] elements)
    {
        AppendLine(string.Format(format, elements), false);
    }
    public void AppendMultilineFormat(string format, params object[] elements)
    {
        AppendLine(string.Format(format, elements), true);
    }

    public override string ToString() => stringBuilder.ToString();

    public readonly struct Region(IndentingStringBuilder builder, string close) : IDisposable
    {
        public readonly void Dispose()
        {
            builder.Dedent();
            builder.AppendLine(close);
        }
    }
    public Region EnterBlock() => EnterIndentedRegion("{", "}");
    public Region EnterBlock(string blockOpener)
    {
        this.AppendLine(blockOpener);
        return EnterIndentedRegion("{", "}");
    }

    public Region EnterIndentedRegion() => EnterIndentedRegion("", "");

    private Region EnterIndentedRegion(string open, string close)
    {
        this.AppendLine(open);
        this.Indent();
        return new Region(this, close);
    }
}