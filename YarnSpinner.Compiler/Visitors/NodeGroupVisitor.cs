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
            if (!context.GetWhenHeaders().Any())
            {
                // The node does not contain any 'when' headers. It's not part
                // of a node group, and doesn't need to be modified.
                return base.VisitNode(context);
            }

            // Get the node's title information.
            if (!Utility.TryGetNodeTitle(SourceFile, context, out var _, out var uniqueTitle, out _, out var nodeGroupName))
            {
                // The node's title or nodegroup name can't be determined.
                return base.VisitNode(context);
            }

            var titleHeader = context.title_header().FirstOrDefault();

            if (titleHeader == null || !context.GetWhenHeaders().Any())
            {
                // The node either lacks a title header, or lacks 'when'
                // headers.
                return base.VisitNode(context);
            }

            // This node contains a title header and at least one 'when' header.
            // It's in a node group. We need to mark it as belonging to a node
            // group and update its title.

            // Add a new header to mark which group it's from, so that this
            // information is available at runtime.
            var groupHeader = new YarnSpinnerParser.HeaderContext(context, 0)
            {
                header_key = new CommonToken(YarnSpinnerParser.ID, Node.NodeGroupHeader),
                header_value = new CommonToken(YarnSpinnerParser.REST_OF_LINE, nodeGroupName)
            };

            context.AddChild(groupHeader);

            // Update the title header to the new 'actual' title.
            titleHeader.title = new CommonToken(YarnSpinnerParser.REST_OF_LINE, uniqueTitle);

            return base.VisitNode(context);
        }
    }
}
