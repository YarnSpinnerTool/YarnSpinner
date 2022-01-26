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
            if (!uri.IsFile) { return Unit.Task; }

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
            
            if (!uri.IsFile) { return Unit.Task; }

            if (!workspace.YarnFiles.TryGetValue(uri, out var yarnDocument))
            {
                // We don't know about this document yet. Hopefully, this first
                // change is a full update (i.e. Range is null, so Text is the
                // full text of the document.)
                //
                // In this case, create a new document and fill it with this
                // content. (If it's not a full update, we have to default to
                // something; in this case, I'm going with the empty string.)

                // Get the first content change
                var firstChange = request.ContentChanges.First();

                // Figure out the the content
                var initialContent = firstChange.Range == null ? firstChange.Text : string.Empty;

                // Create the new document
                yarnDocument = new YarnFileData(initialContent, uri, workspace);
                workspace.YarnFiles[uri] = yarnDocument;
                yarnDocument.Open(initialContent, workspace);
            }

            // Next, go through each content change, and apply it.
            foreach (var contentChange in request.ContentChanges) {
                yarnDocument.ApplyContentChange(contentChange);
            }

            // Finally, update our model using the new content.
            yarnDocument.Update(yarnDocument.Text, workspace);

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