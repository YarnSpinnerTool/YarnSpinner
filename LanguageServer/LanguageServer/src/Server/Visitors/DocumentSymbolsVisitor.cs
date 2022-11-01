using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Yarn.Compiler;

namespace YarnLanguageServer
{
    internal class DocumentSymbolsVisitor : YarnSpinnerParserBaseVisitor<bool>
    {
        // private readonly List<IToken> declaredVariables = new List<IToken>();
        private List<DocumentSymbol> documentSymbols;

        private List<DocumentSymbol> documentSymbolsChildren;
        private YarnFileData yarnFileData;

        protected DocumentSymbolsVisitor(YarnFileData yarnFileData)
        {
            this.yarnFileData = yarnFileData;
            documentSymbols = new List<DocumentSymbol>();
            documentSymbolsChildren = new List<DocumentSymbol>();
        }

        public static IEnumerable<DocumentSymbol> Visit(YarnFileData yarnFileData)
        {
            var visitor = new DocumentSymbolsVisitor(yarnFileData);
            if (yarnFileData.ParseTree != null)
            {
                visitor.Visit(yarnFileData.ParseTree);
            }

            return visitor.documentSymbols;
        }

        public override bool VisitNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            var result = base.VisitNode(context); // Visit Children first

            var title = documentSymbolsChildren.FirstOrDefault(ds => ds.Name == "title")?.Detail ?? "Node";

            var nodeSymbol = new DocumentSymbol
            {
                Kind = SymbolKind.Object,
                Children = documentSymbolsChildren,
                Name = title,
                Range = PositionHelper.GetRange(yarnFileData.LineStarts, context.Start, context.Stop),
                SelectionRange = PositionHelper.GetRange(yarnFileData.LineStarts, context.Start, context.Start),
            };

            this.documentSymbolsChildren = new List<DocumentSymbol>();

            documentSymbols.Add(nodeSymbol);

            return result;
        }

        public override bool VisitHeader([NotNull] YarnSpinnerParser.HeaderContext context)
        {
            var documentSymbol = new DocumentSymbol
            {
                Name = context.header_key.Text,
                Detail = context.header_value?.Text ?? string.Empty,
                Kind = SymbolKind.Property,
                Range = PositionHelper.GetRange(yarnFileData.LineStarts, context.Start, context.Stop),
                SelectionRange = PositionHelper.GetRange(yarnFileData.LineStarts, context.Start, context.Stop),
            };

            documentSymbolsChildren.Add(documentSymbol);
            return base.VisitHeader(context);
        }
    }
}