using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VisualStudioExtension
{
    public class yarnSpinnerContentDefinition
    {
        [Export]
        [Name("yarn")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition yarnSpinnerContentTypeDefinition;

        [Export]
        [FileExtension(".yarn")]
        [ContentType("yarn")]
        internal static FileExtensionToContentTypeDefinition yarnSpinnerFileExtensionDefinition;
    }

    [ContentType("yarn")]
    [Export(typeof(ILanguageClient))]
    class YarnLanguageClient : ILanguageClient
    {
        public string Name => "Yarn Spinner Language Extension";

        public IEnumerable<string> ConfigurationSections => new List<string> { "yarnLanguageServer" };

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => new List<string> { "**/*.yarn" };

        public bool ShowNotificationOnInitializeFailed => true;

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            //Microsoft.VisualStudio.Workspace.

            await Task.Yield();

            ProcessStartInfo info = new ProcessStartInfo();
            //info.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Server", @"YarnLanguageServer.dll");
            info.FileName = "dotnet";

            info.Arguments = "C:/Users/kufu91/Source/Repos/YarnEditorExtensions/LanguageServer/bin/Debug/net5.0/win7-x64/YarnLanguageServer.dll";
            //info.FileName = "C:/Users/kufu91/Source/Repos/YarnEditorExtensions/LanguageServer/bin/Debug/net5.0/win7-x64/YarnLanguageServer.exe";
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WorkingDirectory = "C:/Users/kufu91/Source/Repos/YarnEditorExtensions/LanguageServer/bin/Debug/net5.0/win7-x64/";

            Process process = new Process();
            process.StartInfo = info;

            if (process.Start())
            {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        public async Task OnLoadedAsync()
        {
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializeFailedAsync(Exception e)
        {
            return Task.CompletedTask;
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

#if VS2022

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            return new Task<InitializationFailureContext>(() => new InitializationFailureContext { FailureMessage = initializationState.StatusMessage ?? "OH NO!" });
        }
#endif
    }
}