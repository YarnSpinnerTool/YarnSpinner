using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace YarnLanguageServer.Handlers
{
    internal class WorkspaceSymbolHandler : IWorkspaceSymbolsHandler
    {
        private Workspace workspace;

        public WorkspaceSymbolHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<Container<SymbolInformation>?> Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken)
        {
            var matchingSymbols = workspace.Projects
                .SelectMany(p => p.Files)
                .SelectMany(
                    yarnFile => yarnFile.DocumentSymbols
                    .Where(ds => ds.Name.Contains(request.Query))
                    .Select(ds =>
                        new SymbolInformation
                        {
                            Kind = ds.Kind,
                            Name = ds.Name,
                            Location = new Location
                            {
                                Range = ds.Range,
                                Uri = yarnFile.Uri,
                            },
                        }));

            var result = new Container<SymbolInformation>(matchingSymbols);
            return Task.FromResult<Container<SymbolInformation>?>(result);
        }

        public WorkspaceSymbolRegistrationOptions GetRegistrationOptions(WorkspaceSymbolCapability capability, ClientCapabilities clientCapabilities)
        {
            return new WorkspaceSymbolRegistrationOptions { };
        }
    }
}
