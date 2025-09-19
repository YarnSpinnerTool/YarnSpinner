using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YarnLanguageServer.Handlers
{
    internal class HoverHandler : IHoverHandler
    {
        private readonly Workspace workspace;

        public HoverHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnFile = project?.GetFileData(uri);

            if (yarnFile == null || project == null)
            {
                return Task.FromResult<Hover?>(null);
            }

            (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);

            if (token == null)
            {
                // No idea what this token is.
                return Task.FromResult<Hover?>(null);
            }

            switch (tokenType)
            {
                case YarnSymbolType.Command:
                case YarnSymbolType.Function:
                    var definitions = project.FindActions(token.Text, ActionType.Command).Concat(project.FindActions(token.Text, ActionType.Function));
                    if (definitions.Any())
                    {
                        var definition = definitions.First();

                        var content = new List<MarkedString>();
                        content.Add(new MarkedString("text", definition.YarnName));

                        if (definition.Signature != null)
                        {
                            content.Add(new MarkedString(definition.Language, definition.Signature));
                        }

                        if (definition.Documentation != null)
                        {
                            content.Add(new MarkedString("text", definition.Documentation ?? string.Empty));
                        }

                        var result = new Hover
                        {
                            Contents = new MarkedStringsOrMarkupContent(
                                content.ToArray()),
                            Range = PositionHelper.GetRange(yarnFile.LineStarts, token),
                        };
                        return Task.FromResult<Hover?>(result);
                    }

                    break;

                case YarnSymbolType.Variable:
                    var variableDeclarations = project.FindVariables(token.Text);
                    if (variableDeclarations.Any())
                    {
                        var declaration = variableDeclarations
                            .OrderBy(v =>
                                v.SourceFileName == request.TextDocument.Uri ? // definitions in the current file get priority
                                    Math.Abs(token.Line - v.Range.Start.Line) // within a file, closest definition wins
                                    : 100_000) // don't care what order out of current file definitions come in
                            .First();

                        DeclarationHelper.GetDeclarationInfo(declaration, out var type, out var defaultValue);

                        bool isSmartVariable = declaration.IsInlineExpansion;

                        var descriptionBuilder = new System.Text.StringBuilder()
                            .AppendLine($"{(isSmartVariable ? "Smart Variable" : "Variable")}: `{declaration.Name ?? "(unknown)"} : {type}`")
                            .AppendLine()
                            .AppendLine(declaration.Description)
                            .AppendLine();

                        if (isSmartVariable && declaration.InitialValueParserContext != null)
                        {
                            descriptionBuilder.AppendFormat($"Value: `{declaration.InitialValueParserContext.GetTextWithWhitespace()}`");
                        }
                        else
                        {
                            descriptionBuilder.Append($"Initial value: `{defaultValue}`");
                        }

                        var description = descriptionBuilder.ToString();

                        var result = new Hover
                        {
                            Contents = new MarkedStringsOrMarkupContent(
                                new MarkupContent
                                {
                                    Kind = MarkupKind.Markdown,
                                    Value = description,
                                }
                            ),
                            Range = PositionHelper.GetRange(yarnFile.LineStarts, token),
                        };
                        return Task.FromResult<Hover?>(result);
                    }

                    break;

                // Only supports command/variable hovers for now
                case YarnSymbolType.Node:
                case YarnSymbolType.Unknown:
                    break;
            }

            return Task.FromResult<Hover?>(null);
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
