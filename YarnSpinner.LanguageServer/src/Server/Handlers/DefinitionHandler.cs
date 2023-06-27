using System.Collections.Generic;
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
            System.Uri documentUri = request.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(documentUri).FirstOrDefault();
            var yarnFile = project?.GetFileData(documentUri);

            if (yarnFile == null || project == null)
            {
                return Task.FromResult(new LocationOrLocationLinks());
            }

            (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);

            IEnumerable<Action> functionDefinitionMatches;

            if (token == null) {
                return Task.FromResult(new LocationOrLocationLinks());
            }

            switch (tokenType)
            {
                case YarnSymbolType.Command:
                    functionDefinitionMatches = project.FindActions(token.Text, ActionType.Command, fuzzySearch: false);

                    var locations = functionDefinitionMatches
                        .Where(definition => definition.SourceFileUri != null 
                            && definition.SourceRange != null)
                        .Select(definition =>
                        new LocationOrLocationLink(new Location
                        {
                            Uri = definition.SourceFileUri!,
                            Range = definition.SourceRange!,
                        })
                    );
                    return Task.FromResult(new LocationOrLocationLinks(locations));

                case YarnSymbolType.Function:
                    functionDefinitionMatches = project.FindActions(token.Text, ActionType.Function, fuzzySearch: false);

                    locations = functionDefinitionMatches
                        .Where(definition => definition.SourceFileUri != null
                            && definition.SourceRange != null)
                        .Select(definition =>
                        new LocationOrLocationLink(new Location
                        {
                            Uri = definition.SourceFileUri!,
                            Range = definition.SourceRange!,
                        })
                    );

                    return Task.FromResult(new LocationOrLocationLinks(locations));

                case YarnSymbolType.Variable:

                    var vDefinitionMatches = project.Variables
                        .Where(dv => dv.Name == token.Text)
                        .Select(d => (Uri: d.SourceFileName, d.Range));

                    locations = vDefinitionMatches.Select(definition =>
                        new LocationOrLocationLink(
                            new Location
                            {
                                Uri = definition.Uri,
                                Range = PositionHelper.GetRange(definition.Range),
                            }
                        )
                    );
                    return Task.FromResult<LocationOrLocationLinks>(new LocationOrLocationLinks(locations));

                case YarnSymbolType.Node:
                    var nDefinitionMatches = project.Nodes
                        .Where(nt => nt.Title == token.Text);

                    locations = nDefinitionMatches.Select(definition =>
                        new LocationOrLocationLink(
                            new Location
                            {
                                Uri = definition.File.Uri,
                                Range = definition.TitleHeaderRange,
                            }
                        )
                    );
                    return Task.FromResult(new LocationOrLocationLinks(locations));
            }

            return Task.FromResult(new LocationOrLocationLinks());
        }

        public DefinitionRegistrationOptions GetRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions { DocumentSelector = Utils.YarnDocumentSelector };
        }
    }
}
