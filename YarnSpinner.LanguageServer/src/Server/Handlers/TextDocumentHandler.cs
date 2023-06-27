using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

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

            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();

            if (project == null)
            {
                // We don't know what project handles this URI. Log an error.
                workspace.Window?.LogError($"No known project for URI {uri}");
                return Unit.Task;
            }

            if (project.GetFileData(uri) == null)
            {
                // The file is not already known to the project. Add it to the
                // project.
                project.AddNewFile(uri, request.TextDocument.Text);

                // Adding the document to the project may have changed the
                // current diagnostics.
                workspace.PublishDiagnostics();
            }

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();

            if (!uri.IsFile) { return Unit.Task; }

            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnDocument = project?.GetFileData(uri);

            if (project == null)
            {
                // We don't have a project that includes this URI. Nothing to
                // be done.
                return Unit.Task;
            }

            if (yarnDocument == null)
            {
                // We have a project that owns this URI, but no file data for
                // it. It's likely that this file was just created. Add this
                // file to the project as empty; we will then attempt to apply
                // the content changes to this empty document.
                yarnDocument = project.AddNewFile(uri, string.Empty);
            }

            // Next, go through each content change, and apply it.
            foreach (var contentChange in request.ContentChanges)
            {
                yarnDocument.ApplyContentChange(contentChange);
            }

            // Finally, update our model using the new content.
            yarnDocument.Update(yarnDocument.Text);
            project.CompileProject(
                notifyOnComplete: true,
                Yarn.Compiler.CompilationJob.Type.DeclarationsOnly
            );

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
