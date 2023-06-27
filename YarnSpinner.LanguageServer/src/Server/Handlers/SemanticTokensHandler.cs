using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer.Handlers
{
    internal class SemanticTokensHandler : SemanticTokensHandlerBase
    {
        private Workspace workspace;

        public SemanticTokensHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
        }

        protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            var uri = identifier.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnFile = project?.GetFileData(uri);

            if (yarnFile != null)
            {
                SemanticTokenVisitor.BuildSemanticTokens(builder, yarnFile);
            }
            return Task.CompletedTask;
        }

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                Legend = new SemanticTokensLegend()
                {
                    TokenModifiers = capability?.TokenModifiers,
                    TokenTypes = capability?.TokenTypes,
                },
                Full = new SemanticTokensCapabilityRequestFull
                {
                    Delta = false,
                },
                Range = false,
            };
        }
    }
}
