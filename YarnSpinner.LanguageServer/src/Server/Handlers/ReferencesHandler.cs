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
    internal class ReferencesHandler : IReferencesHandler
    {
        private Workspace workspace;

        public ReferencesHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public static IEnumerable<Location> GetReferences(Project project, string name, YarnSymbolType yarnSymbolType)
        {
            IEnumerable<Location> results;
            Func<YarnFileData, IEnumerable<IToken>> tokenSelector;

            switch (yarnSymbolType)
            {
                case YarnSymbolType.Node:
                    tokenSelector = (yf) => yf.NodeDefinitions.Concat(yf.NodeJumps.Select(j => j.DestinationToken));
                    break;

                case YarnSymbolType.Command:
                    tokenSelector = (yf) => yf.CommandReferences.Select(c => c.NameToken); // maybe add in c# references too
                    break;

                case YarnSymbolType.Variable:
                    tokenSelector = yf => yf.VariableReferences;
                    break;

                default:
                    tokenSelector = (yf) => yf.Tokens;
                    break;
            }

            results = project.Files
                .SelectMany(
                    yf => tokenSelector(yf)
                        .Where(nj => nj?.Text == name)
                        .Select(n => new Location
                        {
                            Uri = yf.Uri,
                            Range = PositionHelper.GetRange(
                                yf.LineStarts,
                                n),
                        })
                    );

            return results;
        }

        public Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnFile = project?.GetFileData(uri);

            if (project == null || yarnFile == null)
            {
                return Task.FromResult(new LocationContainer());
            }

            (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);

            if (tokenType != YarnSymbolType.Unknown && token != null)
            {
                var referenceLocations = GetReferences(project, token.Text, tokenType);
                return Task.FromResult(new LocationContainer(referenceLocations));
            }

            return Task.FromResult(new LocationContainer());
        }

        public ReferenceRegistrationOptions GetRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities)
        {
            return new ReferenceRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                WorkDoneProgress = false,
            };
        }
    }
}
