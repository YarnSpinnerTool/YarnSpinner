using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer.Handlers
{
    internal class SignatureHelpHandler : ISignatureHelpHandler
    {
        private Workspace workspace;

        public SignatureHelpHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            if (workspace.YarnFiles.TryGetValue(request.TextDocument.Uri.ToUri(), out var yarnFile))
            {
                (var info, var parameterIndex) = yarnFile.GetParameterInfo(request.Position);
                if (info.HasValue)
                {
                    var functionInfos = (info.Value.IsCommand ? workspace.GetCommands() : workspace.GetFunctions()).Where(c => c.YarnName == info.Value.Name);
                    IEnumerable<SignatureInformation> results;
                    if (functionInfos.Any())
                    {
                        results = functionInfos.Where(fi => fi.Parameters != null).Select(fi => new SignatureInformation
                        {
                            Label = fi.IsCommand ?
                                $"{fi.YarnName} {string.Join(' ', fi.Parameters.Select(p => $"{(p.DefaultValue.Any()?"[":"")}{p.Type}:{p.Name}{(p.DefaultValue.Any() ? "]" : "")}"))}" :
                                $"{fi.YarnName}( {string.Join(", ", fi.Parameters.Select(p => $"{(p.DefaultValue.Any() ? "[" : "")}{p.Type}:{p.Name}{(p.DefaultValue.Any() ? "]" : "")}"))})",
                            Documentation = fi.Documentation,
                            Parameters =
                                parameterIndex == null ? null : // only list parameters if position is inside a parameter range
                                new Container<ParameterInformation>(fi.Parameters.Select(p =>
                                    new ParameterInformation
                                    {
                                        Label = p.Name,
                                        Documentation = $"{(p.DefaultValue.Any() ? $"Default: {p.DefaultValue}\n" : string.Empty)}{p.Documentation}",
                                    })),
                            ActiveParameter = parameterIndex < fi.Parameters.Count() || !fi.Parameters.Any() || !fi.Parameters.Last().IsParamsArray ?
                                parameterIndex : fi.Parameters.Count() - 1, // if  last param is a params array, it should be the info for all trailing params input
                        });
                    }
                    else
                    {
                        results = new List<SignatureInformation>
                        {
                            new SignatureInformation
                            {
                                Label = info.Value.Name,
                            },
                        };
                    }

                    return Task.FromResult(new SignatureHelp { Signatures = new Container<SignatureInformation>(results) });
                }
            }

            return Task.FromResult<SignatureHelp>(null);
        }

        public SignatureHelpRegistrationOptions GetRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SignatureHelpRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                TriggerCharacters = new Container<string>("(", " "),
                RetriggerCharacters = new Container<string>(" "),
            };
        }
    }
}