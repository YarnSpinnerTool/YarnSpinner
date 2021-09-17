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

        public static IEnumerable<Location> GetReferences(string name, YarnSymbolType yarnSymbolType, Workspace workspace)
        {
            IEnumerable<Location> results;
            Func<YarnFileData, IEnumerable<IToken>> tokenSelector;

            switch (yarnSymbolType)
            {
                case YarnSymbolType.Node:
                    tokenSelector = (yf) => yf.NodeTitles.Concat(yf.NodeJumps);
                    break;

                case YarnSymbolType.Command:
                    tokenSelector = (yf) => yf.Commands; // maybe add in c# references too
                    break;

                case YarnSymbolType.Variable:
                    tokenSelector = yf => yf.Variables;
                    break;

                default:
                    tokenSelector = (yf) => yf.Tokens;
                    break;
            }

            results = workspace.YarnFiles
                        .SelectMany(yf => tokenSelector(yf.Value)
                        .Where(nj => nj.Text == name).Select(n => new Location { Uri = yf.Value.Uri, Range = PositionHelper.GetRange(yf.Value.LineStarts, n) }));

            return results;
        }

        public Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            if (workspace.YarnFiles.TryGetValue(request.TextDocument.Uri.ToUri(), out var yarnFile))
            {
                (var tokenType, var token) = yarnFile.GetTokenAndType(request.Position);

                if (tokenType != YarnSymbolType.Unknown)
                {
                    var referenceLocations = GetReferences(token.Text, tokenType, workspace);
                    return Task.FromResult(new LocationContainer(referenceLocations));
                }
            }

            return Task.FromResult<LocationContainer>(null);
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