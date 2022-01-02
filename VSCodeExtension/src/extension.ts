"use strict";
import * as path from "path";
import { commands, ExtensionContext, Location, Position, Range, Uri, window, workspace } from "vscode";
import { ServerOptions, TransportKind, LanguageClient, LanguageClientOptions } from "vscode-languageclient/node";
import { Trace } from "vscode-jsonrpc/node";
import { config } from "process";
const isDebugMode = () => process.env.VSCODE_DEBUG_MODE === "true";

export async function activate(context: ExtensionContext) {

    // Ensure .net 5.0 is installed and available    
    interface IDotnetAcquireResult {
        dotnetPath: string;
    }
    const commandRes = await commands.executeCommand<IDotnetAcquireResult>('dotnet.acquire', { version: '6.0', requestingExtensionId: 'yarn-spinner-language-server'});
    const dotnetPath = commandRes!.dotnetPath;
    if (!dotnetPath) {
           throw new Error('Could not resolve the dotnet path!');
    }

    const languageServerExe = dotnetPath;
    const languageServerPath =
        isDebugMode() ?
            path.resolve(context.asAbsolutePath("../LanguageServer/bin/Debug/net6.0/YarnLanguageServer.dll")) :
            path.resolve(context.asAbsolutePath("/Server/YarnLanguageServer.dll"));

    let languageServerOptions: ServerOptions = {
        run: {
            command: languageServerExe,
            args: [languageServerPath],
            transport: TransportKind.pipe,
        },
        debug: {
            command: languageServerExe,
            args: [languageServerPath],
            transport: TransportKind.pipe,
            runtime: "",
        },
    };

    var configs = workspace.getConfiguration("yarnLanguageServer");
    let languageClientOptions: LanguageClientOptions = {
        documentSelector: [
            "**/*.yarn"
        ],
        initializationOptions: [
            configs,
        ],
        progressOnInitialization: true,
        synchronize: {
            // configurationSection is deprecated but means we can use the same code for vscode and visual studio (which doesn't support the newer workspace/configuration endpoint)
            configurationSection: 'yarnLanguageServer',
            fileEvents: [workspace.createFileSystemWatcher("**/*.yarn"),workspace.createFileSystemWatcher("**/*.cs"), workspace.createFileSystemWatcher("**/*.ysls.json")]

        },
    };

    const client = new LanguageClient("yarnLanguageServer", "Yarn Language Server", languageServerOptions, languageClientOptions);
    client.trace = Trace.Verbose;

    let disposableClient = client.start();
    // deactivate client on extension deactivation
    context.subscriptions.push(disposableClient);

    // We have to use our own command in order to get the parameters parsed, before passing them into the built in showReferences command.
    async function yarnShowReferences(rawTokenPosition, rawReferenceLocations) {
        var tokenPosition = new Position(rawTokenPosition.Line, rawTokenPosition.Character);
        var referenceLocations = rawReferenceLocations.map(rawLocation => {
            return new Location(
                Uri.parse(rawLocation.Uri),
                new Range(
                    new Position(
                        rawLocation.Range.Start.Line,
                        rawLocation.Range.Start.Character),
                    new Position(
                        rawLocation.Range.End.Line,
                        rawLocation.Range.End.Character)));
        });

        commands.executeCommand('editor.action.showReferences', window.activeTextEditor.document.uri, tokenPosition, referenceLocations);
    }
    context.subscriptions.push(commands.registerCommand('yarn.showReferences', yarnShowReferences));

}