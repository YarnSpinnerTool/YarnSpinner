using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System.Linq;

namespace Yarn.Compiler
{
    internal class NodeGroupVisitor : YarnSpinnerParserBaseVisitor<int>
    {   
        public string SourceFile { get; set; }

        public NodeGroupVisitor(string sourceFile)
        {
            this.SourceFile = sourceFile;
        }

        public override int VisitNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            var titleHeader = context.GetHeader(SpecialHeaderNames.TitleHeader);

            if (titleHeader == null || titleHeader.header_value?.Text == null) {
                // The node doesn't have a title. It can't be part of a node group.
                return base.VisitNode(context); 
            }

            if (context.GetHeaders(SpecialHeaderNames.WhenHeader).Any())
            {
                // This node contains at least one 'when' header. 
                var title = titleHeader.header_value.Text;

                // Add a new header to mark which group it's from.
                var groupHeader = new YarnSpinnerParser.HeaderContext(context, 0)
                {
                    header_key = new CommonToken(YarnSpinnerParser.ID, SpecialHeaderNames.NodeGroupHeader),
                    header_value = new CommonToken(YarnSpinnerParser.REST_OF_LINE, title)
                };

                context.AddChild(groupHeader);

                // Calculate a new unique title for this node and update its title header.
                var newTitle = $"{title}_{CRC32.GetChecksumString(SourceFile + title + context.Start.Line.ToString())}";

                // Update the title header to the new 'actual' title.
                titleHeader.header_value = new CommonToken(YarnSpinnerParser.REST_OF_LINE, newTitle);
            }

            return base.VisitNode(context);
        }
    }
}
