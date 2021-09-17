# Yarn Spinner Language Server
A [Language Server](https://microsoft.github.io/language-server-protocol/) and client extensions for [Yarn Spinner](https://github.com/YarnSpinnerTool/YarnSpinner). Currently, this project supports the latest version of Yarn Spinner ([v2.0.0 Beta 5](https://github.com/YarnSpinnerTool/YarnSpinner/releases/tag/v2.0.0-beta5)), Visual Studio Code, and C# based Yarn Spinner projects. Work is in progress to support Visual Studio 2019, Visual Studio 2022, and Game Maker Studio / Chatterbox based Yarn Spinner projects.

## Installation
To install the Visual Studio Code Extension, download and run the latest vscode vsix installer from this repo's [releases page](https://github.com/pappleby/YarnSpinnerLanguageServer/releases). As part of the first time startup process, the .net 5 runtime may be installed if it cannot be found, so an internet connection may be required at that time.

## Capabilities
This project implements the following language server protocol capabilities:

- Workspace and Document symbol outlines
- Goto definition / references for symbols
- Hover tooltip for commands, functions, and variables
- Semantic token highlighting
- Warning and error diagnostics
- Signature Help for commands and functions
- Code Completion for variables, comands, functions, and (some) keywords
- Code Lenses for nodes indicating reference counts
- Quickfix code actions (only a few right now, but more to come!)

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

