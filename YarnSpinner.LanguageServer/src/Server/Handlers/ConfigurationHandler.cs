using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace YarnLanguageServer.Handlers
{
    internal class ConfigurationHandler : IDidChangeConfigurationHandler
    {
        private Workspace workspace;

        public ConfigurationHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<Unit> Handle(DidChangeConfigurationParams request, CancellationToken cancellationToken)
        {
            if (request.Settings != null)
            {
                request.Settings.ToString();
                if (request.Settings.HasValues)
                {
                    workspace.Configuration.Update(request.Settings);
                }
            }

            return Unit.Task;
        }

        public void SetCapability(DidChangeConfigurationCapability capability, ClientCapabilities clientCapabilities)
        {
            // We don't actually support dynamically changing capabilities yet
            return;
        }
    }
}
