using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer.Handlers
{
    internal class HoverHandler : IHoverHandler
    {
        private Workspace workspace;

        public HoverHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            if (workspace.YarnFiles.TryGetValue(request.TextDocument.Uri.ToUri(), out var yarnFile))
            {
                (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);

                switch (tokenType)
                {
                    case YarnSymbolType.Command:
                    case YarnSymbolType.Function:
                        var definitions = workspace.LookupFunctions(token.Text);
                        if (definitions.Any())
                        {
                            var definition = definitions.First();

                            var result = new Hover
                            {
                                Contents = new MarkedStringsOrMarkupContent(
                                    new MarkedString[]
                                    {
                                        new MarkedString(definition.Language, definition.Signature ?? definition.DefinitionName ?? definition.YarnName),
                                        new MarkedString("text", definition.Documentation ?? string.Empty ),
                                    }),
                                Range = PositionHelper.GetRange(yarnFile.LineStarts, token),
                            };
                            return Task.FromResult(result);
                        }

                        break;

                    case YarnSymbolType.Variable:
                        var variableDefinitions = workspace.GetVariables(token.Text);
                        if (variableDefinitions.Any())
                        {
                            var definition = variableDefinitions
                                .OrderBy(v =>
                                    v.SourceFileName == request.TextDocument.Uri ? // definitions in the current file get priority
                                        Math.Abs(token.Line - v.Range.Start.Line) // within a file, closest definition wins
                                        : 100_000) // don't care what order out of current file definitions come in
                                .First();

                            DeclarationHelper.GetDeclarationInfo(definition, out var type, out var defaultValue);

                            var description = new System.Text.StringBuilder()
                                .AppendFormat("`(variable) {0} : {1}`", definition.Name ?? "<unknown>", type)
                                .AppendLine()
                                .AppendLine()
                                .AppendLine(definition.Description)
                                .AppendLine()
                                .AppendFormat($"Initial value: {defaultValue}")
                                .ToString();

                            var result = new Hover
                            {
                                Contents = new MarkedStringsOrMarkupContent(
                                    new MarkupContent {
                                        Kind = MarkupKind.Markdown,
                                        Value = description,
                                    }
                                ),
                                Range = PositionHelper.GetRange(yarnFile.LineStarts, token),
                            };
                            return Task.FromResult(result);
                        }

                        break;

                    // Only supports command/variable hovers for now
                    case YarnSymbolType.Node:
                    case YarnSymbolType.Unknown:
                        break;
                }
            }

            return Task.FromResult<Hover>(null);
        }

        public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
        {
            return new HoverRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                WorkDoneProgress = false,
            };
        }
    }
}