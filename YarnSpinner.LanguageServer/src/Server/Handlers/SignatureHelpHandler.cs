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

        public Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.ToUri();
            var project = workspace.GetProjectsForUri(uri).FirstOrDefault();
            var yarnFile = project?.GetFileData(uri);

            if (yarnFile == null || project == null)
            {
                return Task.FromResult<SignatureHelp?>(null);
            }

            (var info, var parameterIndex) = yarnFile.GetParameterInfo(request.Position);
            if (info.HasValue)
            {
                var functionInfos = (info.Value.IsCommand ? project.Commands : project.Functions).Where(c => c.YarnName == info.Value.Name);
                IEnumerable<SignatureInformation> results;
                if (functionInfos.Any())
                {
                    results = functionInfos.Where(fi => fi.Parameters != null).Select(fi =>
                    {
                        string functionSeparator = fi.Type == ActionType.Command ? " " : ", ";

                        string signature = string.Join(
                            functionSeparator, fi.Parameters.Select(p => 
                                $"{(p.DisplayDefaultValue.Any() ? "[" : string.Empty)}{p.DisplayTypeName}:{p.Name}{(p.DisplayDefaultValue.Any() ? "]" : string.Empty)}"
                            )
                        );

                        return new SignatureInformation
                        {
                            Label = $"{fi.YarnName} {signature}",
                            Documentation = fi.Documentation,
                            Parameters =
                                                    parameterIndex == null ? null : // only list parameters if position is inside a parameter range
                                                    new Container<ParameterInformation>(fi.Parameters.Select(p =>
                                                        new ParameterInformation
                                                        {
                                                            Label = p.Name,
                                                            Documentation = $"{(p.DisplayDefaultValue.Any() ? $"Default: {p.DisplayDefaultValue}\n" : string.Empty)}{p.Description}",
                                                        })),
                            ActiveParameter = parameterIndex < fi.Parameters.Count() || !fi.Parameters.Any() || !fi.Parameters.Last().IsParamsArray ?
                                                    parameterIndex : fi.Parameters.Count() - 1, // if  last param is a params array, it should be the info for all trailing params input
                        };
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

                return Task.FromResult<SignatureHelp?>(new SignatureHelp {
                    Signatures = new Container<SignatureInformation>(results),
                });
            }

            return Task.FromResult<SignatureHelp?>(null);
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
