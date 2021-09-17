using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace YarnLanguageServer.Handlers
{
    internal class TextDocumentHandler : TextDocumentSyncHandlerBase
    {
        private Workspace workspace;

        public TextDocumentHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        #region Handlers

        public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var text = request.TextDocument.Text;
            if (!workspace.YarnFiles.TryGetValue(uri, out var yarnDocument))
            {
                yarnDocument = new YarnFileData(text, uri, workspace);
                workspace.YarnFiles[uri] = yarnDocument;
            }

            // probably don't need text parameter here, but could be a good sanity check
            yarnDocument.Open(text, workspace);

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var text = request.ContentChanges.First().Text;
            if (!workspace.YarnFiles.TryGetValue(uri, out var yarnDocument))
            {
                yarnDocument = new YarnFileData(text, uri, workspace);
                workspace.YarnFiles[uri] = yarnDocument;
                yarnDocument.Open(text, workspace);
            }

            yarnDocument.Update(text, workspace);

            return Unit.Task;
        }

        #endregion Handlers

        #region Unused Handlers

        public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        #endregion Unused Handlers

        #region Configuration

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            // For now only handling changes to yarn files.
            // Will probably have to look at file extension and switch to support csharp in the future
            return new TextDocumentAttributes(uri, Utils.YarnLanguageID);
        }

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSyncRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                Change = TextDocumentSyncKind.Full,
                Save = new SaveOptions { IncludeText = true },
            };
        }

        #endregion Configuration

    }
}