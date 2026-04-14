
using System.Collections.Generic;
using System.Linq;
using Yarn.Compiler;

/// <summary>
/// Line tag generators are responsible for producing unique line IDs in a Yarn
/// Spinner project.
/// </summary>
public interface ILineTagGenerator
{
    /// <summary>
    /// Represents a situation where the tagger was unable to create a tag for the line.
    /// </summary>
    public class LineTaggingException: System.Exception
    {
        /// <summary>
        /// Creates a new LineTaggingException
        /// </summary>
        public LineTaggingException() {}
        
        /// <inheritdoc/>
        public LineTaggingException(string message): base(message) {}
        
        /// <inheritdoc/>
        public LineTaggingException(string message, System.Exception innerException): base(message, innerException) {}

        /// <inheritdoc/>
        public LineTaggingException(string message, string? SourceFile, int LineNumber)
        {
            this.SourceFile = SourceFile;
            this.LineNumber = LineNumber;
            this._message = message;
        }

        // turns out you can't override the message property on exceptions
        // TIL
        private readonly string? _message;
        
        /// <inheritdoc/>
        public override string? Message
        {
            get
            {
                if (_message != null)
                {
                    return _message;
                }
                else
                {
                    return base.Message;
                }
            }
        }

        /// <summary>
        /// The Yarn file that caused that the tagging exception.
        /// </summary>
        public string? SourceFile;

        /// <summary>
        /// The line number inside of <cref name="SourceFile"/> that caused the exception
        /// </summary>
        public int LineNumber = -1;
    }

    /// <summary>
    /// Represents how Yarn Spinner should handle encountering a <cref name="LineTaggingException"/> exception during tagging.
    /// </summary>
    public enum TagAbortBehaviour
    {
        /// <summary>
        /// The line tagging will be fully aborted. Any changes across all files will be undone.
        /// </summary>
        EntireTagging,
        /// <summary>
        /// The line tagging for the current node will be aborted. Any changes inside the current node will be undone but other nodes in the same and other files will be preserved.
        /// </summary>
        CurrentNode,
        /// <summary>
        /// The line tagging for the current line will be aborted. All changes in the current node, and all other nodes and files will be preserved.
        /// </summary>
        CurrentLine,
    }

    /// <summary>
    /// Contains contextual information needed for generating a line tag.
    /// </summary>
    public struct LineTagContext
    {
        /// <summary>
        /// The current line parse statement.
        /// <remarks>
        /// This gives your custom taggers access to the same information we use internally, unlikely you'll need this but it exists just in case.
        /// A lot of the information contained within the context is already derived from this.
        /// </remarks>
        /// </summary>
        public YarnSpinnerParser.Line_statementContext Line { get; internal set; }

        /// <summary>
        /// The line number inside the file for this specifc line of dialogue.
        /// This matches the line number as seen inside an editor.
        /// </summary>
        /// <remarks>
        /// While this can be used as part of your line id generation this is mostly intended to be used for error messaging, as line numbers are very rarely stable.
        /// It is highly unlikely for this value to remain the same across multiple tagging runs or edits of Yarn files.
        /// Meaning your tagger might have unexpected behaviour across multiple invocations if you are relying on this value.
        /// </remarks>
        public readonly int LineNumber => Line.Start.Line - 1;

        /// <summary>
        /// The name of the file currently being processed.
        /// <remarks>
        /// If the tagging is requested as part of string based compilation instead of a file compilation this value will always be &lt;input&gt;.
        /// </remarks>
        /// </summary>
        public string? SourceFileName { get; internal set; }

        /// <summary>
        /// The text of the line of dialogue
        /// </summary>
        public string LineText
        {
            get
            {
                StringTableGeneratorVisitor.GenerateFormattedText(
                    this.Line.line_formatted_text().children,
                    out var composedString,
                    out _);

                return composedString;
            }
        }

        /// <summary>
        /// The line id of the current line, will only have a value for lines that are already tagged.
        /// </summary>
        public string? LineID;
    };

    /// <summary>
    /// Generates a unique line tag.
    /// </summary>
    /// <param name="node">The node in which the line exist</param>
    /// <param name="lineIndex">The relative line number of the dialogue.</param>
    /// <returns>The generated line tag.</returns>
    public string GenerateLineTag(string node, int lineIndex);

    /// <summary>
    /// Called by Yarn Spinner before any line generation is
    /// </summary>
    /// <param name="LineContexts">The combined information of all nodes and lines</param>
    public void PrepareForLines(Dictionary<string, List<ILineTagGenerator.LineTagContext>> LineContexts);
}
