using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer.Handlers
{
    internal class RenameHandler : IRenameHandler, IPrepareRenameHandler
    {
        private Workspace workspace;

        public RenameHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnFile = project?.GetFileData(uri);

            if (project == null || yarnFile == null)
            {
                return Task.FromResult<WorkspaceEdit?>(null);
            }

            (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);
            if (tokenType != YarnSymbolType.Unknown && token != null)
            {
                if (IsInvalidNewName(tokenType, request.NewName, out var message))
                {
                    throw new OmniSharp.Extensions.JsonRpc.RpcErrorException(400, null, message);
                }

                var referenceLocations = ReferencesHandler
                    .GetReferences(project, token.Text, tokenType)
                    .GroupBy(ls => ls.Uri);

                var t = referenceLocations.Select(locationsGroup =>
                {
                    var edits = locationsGroup.Select(location => new TextEdit { Range = location.Range, NewText = request.NewName });
                    var tde = new TextDocumentEdit
                    {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = locationsGroup.Key },
                        Edits = new TextEditContainer(edits),
                    };
                    return new WorkspaceEditDocumentChange(tde);
                });

                var result = new WorkspaceEdit
                {
                    DocumentChanges = t.ToArray(),
                };

                return Task.FromResult<WorkspaceEdit?>(result);
            }

            return Task.FromResult<WorkspaceEdit?>(null);
        }

        public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
        {
            return new RenameRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                PrepareProvider = true,
                WorkDoneProgress = false,
            };
        }

        public Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnFile = project?.GetFileData(uri);

            if (yarnFile == null)
            {
                return Task.FromResult<RangeOrPlaceholderRange?>(null);
            }

            (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);
            if (tokenType != YarnSymbolType.Unknown && token != null)
            {
                var range = PositionHelper.GetRange(yarnFile.LineStarts, token);
                return Task.FromResult<RangeOrPlaceholderRange?>(new RangeOrPlaceholderRange(range));
            }

            return Task.FromResult<RangeOrPlaceholderRange?>(null);
        }

        private static bool IsInvalidNewName(YarnSymbolType symbolType, string newName, out string message)
        {
            if (symbolType == YarnSymbolType.Variable && !newName.StartsWith('$'))
            {
                message = "Variable names must start with $ character";
                return true;
            }

            if ((symbolType == YarnSymbolType.Variable && newName.LastIndexOf('$') != 0)
             || (symbolType != YarnSymbolType.Variable && newName.Contains('$')))
            {
                message = "Invalid character $ found.";
                return true;
            }

            if (newName.Contains(' '))
            {
                message = "Spaces are not valid characters here.";
                return true;
            }

            message = string.Empty;
            return false;
        }
    }
}
