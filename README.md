# Yarn Spinner Language Server
A [Language Server](https://microsoft.github.io/language-server-protocol/) and client extensions for [Yarn Spinner](https://github.com/YarnSpinnerTool/YarnSpinner). Currently, this project supports the latest version of Yarn Spinner ([v2.0.0 Beta 5](https://github.com/YarnSpinnerTool/YarnSpinner/releases/tag/v2.0.0-beta5)), Visual Studio Code, and C# based Yarn Spinner projects. Work is in progress to support Visual Studio 2019, Visual Studio 2022, and Game Maker Studio / Chatterbox based Yarn Spinner projects.

## Installation
To install visit the [Visual Studio Code Extension Marketplace](https://marketplace.visualstudio.com/items?itemName=Pappleby.yarn-spinner-language-server). To manually install Visual Studio Code Extension, download and install the latest vscode vsix installer from this repo's [releases page](https://github.com/pappleby/YarnSpinnerLanguageServer/releases). Note that you need to use the [Install from VSIX](https://user-images.githubusercontent.com/408888/133859287-0ec32501-a711-4ed4-922c-cc7e3c788783.png) command from VSCode. As part of the first time startup process, the .net 5 runtime may be installed if it cannot be found, so an internet connection may be required at that time.


## Capabilities
This project implements the following language server protocol capabilities:

- Workspace and Document symbol outlines
- Goto definition / references for symbols
- Hover tooltip for commands, functions, and variables
- Semantic token highlighting
- Warning and error diagnostics
- Signature Help for commands and functions
- Code Completion for variables, commands, functions, and (some) keywords
- Code Lenses for nodes indicating reference counts
- Quickfix code actions (only a few right now, but more to come!)

## Importing / Overriding via JSON
If you want to import command and function definitions for a language other than C#, or you want to override information that the language server parses from C#, add a JSON file with the extension ".ysls.json" to your project's folder using the [ysls.json schema](/LanguageServer/src/Server/Documentation/ysls.schema.json). 

For examples, take a look at this [import example](/LanguageServer/ImportExample.ysls.json) or the [yarn spinner built in Commands and Functions file](/LanguageServer/src/Server/Documentation/BuiltInFunctionsAndCommands.ysls.json). 

One caveat to note; the language server does not automatically reload ysls.json files (nor .cs files). To force the server to reload function and command definitions, go to the extension settings and toggle the "CSharp Lookup" property. (This will be fixed in an upcoming release!)


## Demo
<img src="https://user-images.githubusercontent.com/408888/133907128-ab3fe7a3-b2cf-4ce6-98d7-65f048fbae1f.gif" alt="Demonstration of hover tooltip, Go to references, and Go to definition" />

<img src="https://user-images.githubusercontent.com/408888/133907396-9cabe05b-bdf8-44d3-a8df-6e44e55fab98.gif" alt="Demonstration of command suggestions, signature help, and parameter count checking" />


## Project organization
This project is composed of 5 subprojects:
- **LanguageServer**: The bulk of the project, consisting of 
  - Server: Implementation of the language server protocol, using [OmniSharp.Extensions.LanguageServer.Server](https://github.com/OmniSharp/csharp-language-server-protocol) as a framework
  - Compiler: 
    - A version of [YarnSpinner.Compiler](https://github.com/YarnSpinnerTool/YarnSpinner/tree/main/YarnSpinner.Compiler) with slight modifications to tolerate parsing errors and support language server features
    - The C# port of the [antlr-c3](https://github.com/mike-lischke/antlr4-c3) Code Completion Core
- **VSCodeExtension**: Visual Studio Code extension to launch / connect to the language server and ensure server dependencies are available
- **VisualStudioExtensionBase**: A shared project to avoid duplication between the VS2019 and VS2022 versions of the client extension
- **VisualStudio2022Extension**: In progress code for a VS2022 client extension for the language server.
- **VisualStudio2019Extension**: In progress code for a VS2019 client extension for the language server.

