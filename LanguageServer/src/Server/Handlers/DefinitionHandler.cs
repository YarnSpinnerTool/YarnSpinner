using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer.Handlers
{
    internal class DefinitionHandler : IDefinitionHandler
    {
        private Workspace workspace;

        public DefinitionHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            if (workspace.YarnFiles.TryGetValue(request.TextDocument.Uri.ToUri(), out var yarnFile))
            {
                (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);

                switch (tokenType)
                {
                    case YarnSymbolType.Command:
                        var functionDefinitionMatchs = workspace.LookupFunctions(token.Text).Where(f => f.IsCommand);

                        var locations = functionDefinitionMatchs.Select(definition =>
                            new LocationOrLocationLink(new Location { Uri = definition.DefinitionFile, Range = definition.DefinitionRange }));
                        return Task.FromResult<LocationOrLocationLinks>(new LocationOrLocationLinks(locations));

                    case YarnSymbolType.Function:
                        functionDefinitionMatchs = workspace.LookupFunctions(token.Text).Where(f => !f.IsCommand);

                        locations = functionDefinitionMatchs.Select(definition =>
                            new LocationOrLocationLink(new Location { Uri = definition.DefinitionFile, Range = definition.DefinitionRange })
                        );

                        return Task.FromResult<LocationOrLocationLinks>(new LocationOrLocationLinks(locations));

                    case YarnSymbolType.Variable:
                        var vDefinitionMatches = workspace.YarnFiles.Values.SelectMany(yf => yf.DeclaredVariables.Where(dv => dv.Name == token.Text).Select(t => (yf.Uri, t.DefinitionRange)));
                        locations = vDefinitionMatches.Select(definition =>
                            new LocationOrLocationLink(new Location { Uri = definition.Uri, Range = definition.Item2 })
                        );
                        return Task.FromResult<LocationOrLocationLinks>(new LocationOrLocationLinks(locations));

                    case YarnSymbolType.Node:
                        var nDefinitionMatches = workspace.GetNodeTitles().Where(nt => nt.title == token.Text);
                        locations = nDefinitionMatches.Select(definition =>
                            new LocationOrLocationLink(new Location { Uri = definition.uri, Range = definition.range })
                        );
                        return Task.FromResult<LocationOrLocationLinks>(new LocationOrLocationLinks(locations));
                }
            }

            return Task.FromResult<LocationOrLocationLinks>(null);
        }

        public DefinitionRegistrationOptions GetRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions { DocumentSelector = Utils.YarnDocumentSelector };
        }
    }
}