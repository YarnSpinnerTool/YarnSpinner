using System.Linq;
using Antlr4.Runtime.Tree;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Yarn.Compiler;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace YarnLanguageServer
{
    internal class TokenPositionVisitor : YarnSpinnerParserBaseVisitor<int?>
    {
        private readonly Position position;

        public TokenPositionVisitor(Position position)
        {
            this.position = position;
        }

        public static int? Visit(YarnFileData yarnFileData, Position position)
        {
            var visitor = new TokenPositionVisitor(position);
            if (yarnFileData.ParseTree != null)
            {
                return visitor.Visit(yarnFileData.ParseTree);
            }

            return null;
        }

        public override int? VisitChildren(IRuleNode node)
        {
            foreach (var childi in Enumerable.Range(0, node.ChildCount))
            {
                var result = Visit(node.GetChild(childi));
                if (result.HasValue) {
                    return result;
                }
            }

            return null;
        }

        public override int? VisitTerminal(ITerminalNode node)
        {
            if (PositionHelper.DoesPositionContainToken(position, node.Symbol))
            {
                return node.Symbol.TokenIndex;
            }

            return null;
        }
    }
}