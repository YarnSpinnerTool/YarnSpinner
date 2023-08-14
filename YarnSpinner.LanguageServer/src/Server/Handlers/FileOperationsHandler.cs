using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Antlr4.Runtime;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace YarnLanguageServer.Handlers
{
    internal class FileOperationsHandler : IDidChangeWatchedFilesHandler
    {
        private Workspace workspace;

        public FileOperationsHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
        {
            var yarnChanges = request.Changes.Where(c => c.Uri.Path.EndsWith(".yarn"));
            var csChanges = request.Changes.Where(c => c.Uri.Path.EndsWith(".cs"));
            var jsonChanges = request.Changes.Where(c => c.Uri.Path.EndsWith(".ysls.json"));
            var yarnProjectChanges = request.Changes.Where(c => c.Uri.Path.EndsWith(".yarnproject"));

            bool needsWorkspaceReload = false;

            // Any change to a Yarn project requires that we rebuild the entire
            // workspace
            if (yarnProjectChanges.Any()) {
                needsWorkspaceReload = true;
            }

            // This is probably wordiest way to do this,
            // but these cases will become different as we replace the "redo everything" strategy with something more incremental
            foreach (var change in yarnChanges)
            {
                switch (change.Type)
                {
                    case FileChangeType.Created:
                        needsWorkspaceReload = true;
                        break;
                    case FileChangeType.Deleted:
                        needsWorkspaceReload = true;
                        break;
                }
            }

            foreach (var change in csChanges)
            {
                switch (change.Type)
                {
                    case FileChangeType.Changed:
                        needsWorkspaceReload = true;
                        break;
                    case FileChangeType.Deleted:
                        needsWorkspaceReload = true;
                        break;
                }
            }

            foreach (var change in jsonChanges)
            {
                switch (change.Type)
                {
                    case FileChangeType.Created:
                        break;
                    case FileChangeType.Changed: // TODO: Consider only accepting changed files that adhere to ysls schema
                        needsWorkspaceReload = true;
                        break;
                    case FileChangeType.Deleted:
                        needsWorkspaceReload = true;
                        break;
                }
            }

            if (needsWorkspaceReload)
            {
                workspace.ReloadWorkspace();
            }

            return Unit.Task;
        }

        public DidChangeWatchedFilesRegistrationOptions GetRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DidChangeWatchedFilesRegistrationOptions
            {
                Watchers = new Container<FileSystemWatcher>(
                               new FileSystemWatcher()
                               {
                                   Kind = WatchKind.Create | WatchKind.Delete, // Don't watch on change, we already track that with text operations
                                   GlobPattern = Utils.YarnSelectorPattern,
                               },
                               new FileSystemWatcher()
                               {
                                   Kind = WatchKind.Change | WatchKind.Delete,
                                   GlobPattern = Utils.CSharpSelectorPattern,
                               },
                               new FileSystemWatcher()
                               {
                                   Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
                                   GlobPattern = Utils.YslsJsonSelectorPattern,
                               }
                           ),
            };
        }
    }
}
