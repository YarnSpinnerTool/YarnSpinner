using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer.Handlers
{
    internal class CodeLensHandler : ICodeLensHandler
    {
        private Workspace workspace;

        public CodeLensHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<CodeLensContainer> Handle(CodeLensParams request, CancellationToken cancellationToken)
        {
            if (workspace.YarnFiles.TryGetValue(request.TextDocument.Uri.ToUri(), out var yarnFile))
            {
                var results = yarnFile.NodeDefinitions.Select(titleToken =>
                   {
                       var referenceLocations = ReferencesHandler.GetReferences(titleToken.Text, YarnSymbolType.Node, workspace);
                       var count = referenceLocations.Count() - 1; // This is a count of 'other' references, so don't include the declaration

                       // OmniSharp Locations, Ranges and Positions have
                       // PascalCase property names, but the LSP wants
                       // camelCase. Provide our own serialization here to
                       // ensure this.
                       var serializer = new Newtonsoft.Json.JsonSerializer
                       {
                           ContractResolver = new CamelCasePropertyNamesContractResolver(),
                       };

                       return new CodeLens
                       {
                           Range = PositionHelper.GetRange(yarnFile.LineStarts, titleToken),
                           Command = new Command
                           {
                               Title = count == 1 ? "1 reference" : $"{count} references",
                               Name = Commands.ShowReferences,
                               Arguments = new JArray
                               {
                                    JToken.FromObject(PositionHelper.GetPosition(yarnFile.LineStarts, titleToken.StartIndex), serializer),
                                    JToken.FromObject(referenceLocations, serializer),
                               },
                           },
                       };
                   });

                CodeLensContainer result = new CodeLensContainer(results);
                return Task.FromResult(result);
            }

            return Task.FromResult<CodeLensContainer>(null);
        }

        public CodeLensRegistrationOptions GetRegistrationOptions(CodeLensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CodeLensRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                ResolveProvider = false,
                WorkDoneProgress = false,
            };
        }
    }
}