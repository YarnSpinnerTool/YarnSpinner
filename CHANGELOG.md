# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added a method for manually loading a string table as a dictionary to DialogueRunner
- DialogueUI now allows skipping to the end of a line's delivery, by calling MarkLineComplete before the line has finished appearing.
- Option buttons can now use TextMeshPro Text components, in addition to Unity UI Text components. (TextMeshPro for line display was already supported.)
- DialogueUI now allows other scripts to select an option. When the `SelectOption` method, which takes an integer representing the index of the option you want to select, is called, the Dialogue UI will act as though the corresponding button was clicked.

### Changed

- Made the debug display in InMemoryVariableStorage slightly tidier
- Made changing the InMemoryVariableStorage update its debug display's layout components
- Made InMemoryVariableStorage's contents enumerable in a foreach loop
- Fixed a bug where the Dialog would pause when a blocking command handler immediately calls its onComplete and returns
- Fixed a bug where parsing the `<<wait>>` command's parameter was locale-specific (i.e. certain European locales parse decimal numbers as "1,0"), which meant that behaviour would vary based on the end-user's configuration.
- Fix a bug where manually-added functions would never run if the first parameter was the name of an object in the scene.
- Improve the UI for managing localised lines (thanks to @Schroedingers-Cat)

## [1.0.1] - 2020-01-08

A bugfix release.

### Changed

- Fixed an issue where the first instruction after an `if` statement, option, shortcut option or jump to another node could be skipped.

## [1.0.0] - 2020-01-07

This is the first major release of Yarn Spinner. We're thrilled to bring this to you, and want to thank everyone who's helped us bring Yarn Spinner to this point.

### Added

- **Binary Program Format:** Yarn programs are now compiled into a binary format, which uses Protocol Buffers. Compiled files can be written to disk and loaded at runtime, which means that you don't need to include the source code of your game's dialog when distributing it to players. The time needed to load a dialogue file is also significantly reduced, because compilation happens on your machine, not on the player's.
- **Canvas Prefab:** The `Dialogue` prefab, which you can find in the `YarnSpinner/Prefabs` folder, is a drag-and-drop object that you can add to your scene. It's a great way to get started using Yarn Spinner in your own game, and is designed to be customised to fit your needs.
- **Dialogue UI Events:** The `DialogueUI` class now fires Unity Events when important events occur, like dialogue starting, a line appearing, a line's delivery completing, and more. You can use this to control the behaviour of your dialogue UI without writing any code.
- **Automatic Compilation in Unity:** The Unity integration for Yarn Spinner will automatically detect your Yarn files and compile them.
- **Instant Localisation Tags:** Select the Yarn file in Unity, and click Add Line Tags. Any lines or options that don't have a localisation tag will have one added. (Note that this step changes your files on disk, and can't be undone.)
- **Simpler CSV Export:** When you want to export a CSV file containing your localised lines, select the Yarn file in Unity, choose the language you want to localise into from the drop-down menu, and click Create New Localisation. A `.csv` file will be created next to your Yarn file, ready to be sent to your translators. (Note that you can only create a CSV when every line and option in the file has a line tag. Yarn Spinner in Unity can create them for you if you click Add Line Tags.)
- **Visual Studio Code Extension:** We've heard from people who want to write their Yarn code in a text editor, and we've created an extension for [Visual Studio Code](https://code.visualstudio.com) that adds syntax highlighting support (with more features coming in the future!) You can install the extension from the [Visual Studio Code Marketplace](https://marketplace.visualstudio.com/items?itemName=SecretLab.yarn-spinner).
- **New Website:** A brand-new website for Yarn Spinner is now available at [yarnspinner.dev](https://yarnspinner.dev). This will be the home of all future documentation.

### Changed

- The standard file extension for Yarn codes has changed from `.yarn.txt` to `.yarn`. The Yarn Editor has been updated to save as `.yarn` by default. (It still supports opening your existing `.yarn.txt` files.)
- The `Dialogue` class, which executes your Yarn program, previously sent the text of the lines and options found in the source code. This has now changed; the `Dialogue` will now instead send just the line code, and the `DialogueRunner` matches the code to a localised string.
- If a line doesn't have a line code, Yarn Spinner will create a unique one based on the name of the file, the name of the node, and where the line appears.
- The `Compiler` class's `CompileFile` and `CompileString` methods, which compile `.yarn` files into Yarn programs, have had their method signatures change. They now return a `Yarn.Compiler.Status` enum, and produce *two* results: the compiled Yarn program, and the extracted string table as a dictionary.
- The compiler has been moved into its own assembly, `Yarn.Compiler.dll`. If your code doesn't use any of the classes in the `Yarn.Compiler` namespace, it won't be included. This reduces the amount of code you need to include in your game.
- The `Yarn.Unity.Example` classes, like `ExampleDialogUI`, have been renamed to remove "`Example`". Everyone was using these as the basis for their own classes anyway, and we felt it was better to acknowledge that they weren't really showing *one* way to do it, but rather showing the *preferred* way. This name change acknowledges this fact.
- `DialogueRunner` now no longer relies on coroutines for operations that take longer than a single frame. Instead, `DialogueUI`'s methods that run in response to lines, options and commands return a `Dialogue.HandlerExecutionType` enum to indicate to Yarn Spinner whether it should pause execution or continue running.
- `DialogueRunner` now separates out the act of loading compiled programs and the act of loading a string table into two distinct methods. This gives you control over which localised lines of text should be used when the `Dialogue` class sends line codes to your game.

### Removed

- We've removed the "simple dialog example" from the repo, and made the "complex dialog example" - the one set in space, featuring Sally and the Ship - the sole example.
- We've removed the documentation from the repo; the new home for Yarn Spinner documentation is the official website, [yarnspinner.dev](https://yarnspinner.dev).
