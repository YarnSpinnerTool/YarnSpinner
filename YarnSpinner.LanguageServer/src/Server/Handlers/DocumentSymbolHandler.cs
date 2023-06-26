using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer.Handlers
{
    internal class DocumentSymbolHandler : IDocumentSymbolHandler
    {
        private Workspace workspace;

        public DocumentSymbolHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnDocument = project?.GetFileData(uri);
            if (yarnDocument == null)
            {
                return Task.FromResult(new SymbolInformationOrDocumentSymbolContainer());
            }
            else
            {
                var result = new SymbolInformationOrDocumentSymbolContainer(
                    yarnDocument.DocumentSymbols.Select(
                        ds => new SymbolInformationOrDocumentSymbol(ds)));

                return Task.FromResult(result);
            }
        }

        public DocumentSymbolRegistrationOptions GetRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentSymbolRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
            };
        }
    }
}
