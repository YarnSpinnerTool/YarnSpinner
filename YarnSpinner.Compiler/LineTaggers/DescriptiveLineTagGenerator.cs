using System.Collections.Generic;
using Yarn.Markup;

namespace Yarn.Compiler
{
    public class DescriptiveLineTagGenerator : ILineTagGenerator
    {
        Yarn.Markup.LineParser lineParser = new();

        const int IndexMultiplier = 10;

        public string GenerateLineTag(ILineTagGenerator.LineTagContext context)
        {
            var parentNode = context.Line.GetParentContextOfType<YarnSpinnerParser.NodeContext>()
                ?? throw new System.ArgumentException("Line's node is null");


            Utility.TryGetNodeTitle(context.SourceFileName, parentNode, out _, out var nodeUniqueTitle, out _, out _);

            if (nodeUniqueTitle == null)
            {
                throw new System.InvalidOperationException($"Unable to determine node title for line \"{context.Line}\"");
            }

            var parsedMarkup = lineParser.ParseString(context.LineText, System.Globalization.CultureInfo.InvariantCulture.TwoLetterISOLanguageName);

            List<string> lineIDComponents = new()
            {
                nodeUniqueTitle
            };

            if (parsedMarkup.TryGetAttributeWithName("character", out var characterAttribute)
                && characterAttribute.TryGetProperty("name", out MarkupValue characterNameMarkup)
                && characterNameMarkup.Type == MarkupValueType.String
                )
            {
                lineIDComponents.Add(characterNameMarkup.StringValue);
            }

            lineIDComponents.Add(string.Format("{0:D3}", context.LineIndex * IndexMultiplier));

            return "line:" + string.Join("_", lineIDComponents);
        }
    }

}
