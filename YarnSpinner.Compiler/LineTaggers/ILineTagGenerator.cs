
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
    /// Contains contextual information needed for generating a line tag.
    /// </summary>
    public struct LineTagContext
    {
        public YarnSpinnerParser.Line_statementContext Line { get; internal set; }

        public string? SourceFileName { get; internal set; }

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

        public string? LineID;

        public static LineTagContext GetIndividualContext(Dictionary<string, List<LineTagContext>>? lineContexts, string node, int lineIndex)
        {
            if (lineContexts == null)
            {
                throw new System.ArgumentException($"Asked to generate a line at index {lineIndex} for {node} node but we haven't been given a context");
            }
            if (!lineContexts.TryGetValue(node, out var linesForNode))
            {
                throw new System.ArgumentException($"Asked to generate a line at index {lineIndex} for {node} node but we have no node with this name");
            }
            if (linesForNode.Count == 0)
            {
                throw new System.ArgumentException($"Asked to generate a line at index {lineIndex} for {node} node but this list is empty");
            }
            if (lineIndex < 0)
            {
                throw new System.ArgumentException($"Asked to generate a line at index {lineIndex} for {node} node but the index is out of bounds");
            }
            if (lineIndex >= linesForNode.Count)
            {
                throw new System.ArgumentException($"Asked to generate a line at index {lineIndex} for {node} node but the index is out of bounds");
            }
            var context = linesForNode[lineIndex];
            return context;
        }
    };

    /// <summary>
    /// Generates a unique line tag.
    /// </summary>
    /// <param name="context">Contains information about the line needed to produce a unique line tag.</param>
    /// <returns>The generated line tag.</returns>
    public string GenerateLineTag(string node, int lineIndex);

    public void PrepareForLines(Dictionary<string, List<ILineTagGenerator.LineTagContext>> LineContexts);
}
