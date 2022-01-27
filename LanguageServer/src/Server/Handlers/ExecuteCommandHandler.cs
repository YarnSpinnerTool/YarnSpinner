using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace YarnLanguageServer.Handlers
{
    internal class ExecuteCommandHandler : IExecuteCommandHandler
    {

        private Workspace workspace;

        public ExecuteCommandHandler(Workspace workspace)
        {
            this.workspace = workspace;
        }

        private const string NodeAddCommand = "node.add";
        private const string NodeRemoveCommand = "node.remove";
        private const string NodeUpdateHeaderCommand = "node.updateHeader";

        public ExecuteCommandRegistrationOptions GetRegistrationOptions(ExecuteCommandCapability capability, ClientCapabilities clientCapabilities)
        {
            return new ExecuteCommandRegistrationOptions
            {
                Commands = new[] {
                    NodeAddCommand,
                    NodeRemoveCommand,
                    NodeUpdateHeaderCommand,
                },
            };
        }

        public Task<Unit> Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            switch (request.Command) {
                case NodeAddCommand:
                    return HandleAddCommand(request, cancellationToken);
                case NodeRemoveCommand:
                    return HandleRemoveCommand(request, cancellationToken);
                case NodeUpdateHeaderCommand:
                    return HandleUpdateHeaderCommand(request, cancellationToken);
            }

            return Task.FromResult(Unit.Value);
        }

        private Task<Unit> HandleUpdateHeaderCommand(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private Task<Unit> HandleRemoveCommand(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private Task<Unit> HandleAddCommand(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}