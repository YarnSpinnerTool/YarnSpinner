
using System.Collections.Generic;
using Yarn.Compiler;


/// <summary>
/// Line tag generators are responsible for producing unique line IDs in a Yarn
/// Spinner project.
/// </summary>
public interface ILineTagGenerator
{
    /// <summary>
    /// Contains contextual information needed for generating a line tag.
    /// </summary>
    public struct LineTagContext
    {
        /// <summary>
        /// The line IDs that already exist in the project, and must not be
        /// duplicated.
        /// </summary>
        public HashSet<string>? ExistingLineIDs { get; internal set; }

        public YarnSpinnerParser.Line_statementContext Line { get; internal set; }

        public string? SourceFileName { get; internal set; }
        public int LineIndex { get; internal set; }

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
    };

    /// <summary>
    /// Generates a unique line tag.
    /// </summary>
    /// <param name="context">Contains information about the line needed to produce a unique line tag.</param>
    /// <returns>The generated line tag.</returns>
    public string GenerateLineTag(LineTagContext context);
}
