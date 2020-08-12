# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added support for arbitrary variable names, using backticks.

### Changed

### Removed

## [v1.2.0] 2020-05-04

### Added

- Added Nuget package definitions for [YarnSpinner](http://nuget.org/packages/YarnSpinner/) and [YarnSpinner.Compiler](http://nuget.org/packages/YarnSpinner.Compiler/).

### Changed

- Parse errors no longer show debugging information in non-debug builds.

### Removed

## [v1.2.0-beta1] 2020-05-28

### Added

- Yarn scripts now appear with Yarn Spinner icon. (@Schroedingers-Cat)
- Documentation is updated to reflect the current version number (also to mention 2018.4 LTS as supported)
- Added a button in the Inspector for `.yarn` files in Yarn Spinner for Unity, which updates localised `.csv` files when the `.yarn` file changes. (@stalhandske, #227)
- Added handlers for when nodes begin executing (in addition to the existing handlers for when nodes complete.) (@arendhil, #222)
- `OptionSet.Option` now includes the name of the node that an option will jump to if selected.
- Added unit tests for Yarn Spinner for Unity (@Schroedingers-Cat)
- Yarn Spinner for Unity: Added a menu item for creating new Yarn scripts (Assets -> Create -> Yarn Script)

### Changed

- Fixed a crash in the compiler when parsing single-character commands (e.g. `<<p>>`) (#231)

### Removed

## [v1.1.0] - 2020-04-01

Final release of v1.1.0.

## [v1.1.0-beta3]

### Added

### Changed

- Fixed a bug that caused `<<else>>` to be incorrectly parsed as a command, not an `else` statement, which meant that flow control didn't work correctly.

## [v1.0.0-beta2]

### Added

- **Inline Expressions**: Embed variables, values and expressions right into your dialogue.
  - You can use inline expressions in lines, options, shortcut options, and commands.
  - Inline expressions look like this: `Mae: Wow! I have {$num_pies} pies!`.
  - When the compiler processes a line that includes an inline expression, the line that's stored in the string table will have each of the expressions replaced with a placeholder. For example, the line above will be stored as `Mae: Wow! I have {0} pies!`. If you're translating a line to other languages, the placeholders can be moved and re-ordered as you need them.
  - Any expression can be used - numbers, strings, variables, function calls, or more complex expressions.
  - The `Line` struct now includes an array of substitutions, which Dialogue UI objects will insert into the localised line at the appropriate place.
  - Documentation for inline expressions is available on the [Yarn Spinner site](https://yarnspinner.dev/docs/syntax/#inline-expressions).
- **Format Functions**: Easier localisation when dealing with inline expressions.
  - Format functions are in-line expressions in your scripts that dynamically select text based on a variable. These functions can be localised, which means you can change them based on the needs of the language you're translating the game into.
  - Format functions will appear as-is in the .csv string tables that Yarn Spinner for Unity generates, which means that they can be edited by translators.  
  - Please note that format functions are intended to be a tool for ensuring correct grammar across multiple languages. They are more complex than a simple inline expression, and may complicate your dialogue. They're not intended to replace `if`-`endif` structures for your dialogue's logic.
  - There are three format functions available: `select`, `plural`, and `ordinal`.
  - The `select` function takes a string variable and uses its value to select a piece of text to use. For example:
    - `Character: Wow, [select {$gender} male="he" female="she" other="they"] seem happy!`
  - The `plural` function uses a number variable and determines its plural category. For example:
    - `Character: Good thing I have {$money_count} gold [plural {$money_count} one="piece" other="pieces"]!`
  - The `ordinal` function uses a number variable and determines its ordinal category. For example:
    - `Character: The race is over! I came [ordinal {$race_position} one="%st" two="%nd" few="%rd" other="%th"]!`
    - This example also shows how you can embed the variable that the function is using in the result - the `%` character will be replaced the variable's value (in this example, `$race_position`, creating text like "I came 3rd!")
  - Different languages have different plural rules. Yarn Spinner uses the plural rules defined by the [Unicode CLDR](https://www.unicode.org/cldr/charts/latest/supplemental/language_plural_rules.html); note that not all languages make use of all plural categories.
    - Yarn Spinner for Unity will use the Text Language setting to determine which plural rules to apply.
  - Documentation for format functions is available on the [Yarn Spinner site](https://yarnspinner.dev/docs/syntax/#format-functions).
- **Faster Compiling:** Yarn Spinner for Unity now uses .asmdef files. 
  - Yarn Spinner's Unity code now compiles to a separate assembly. (@Schroedingers-Cat)
  - **IMPORTANT:** if you're using asmdefs in your own code, any assembly you write that needs to refer to Yarn Spinner will need to add a reference to the YarnSpinner.Unity assembly.
- **Patreon Supporter Info**: [Patreon supporter](https://www.patreon.com/bePatron?u=11132340) information is now displayed in the Yarn Spinner window in Yarn Spinner for Unity. 
  - To view it, open the Window menu, and choose Yarn Spinner.
  - While you're viewing it, why not consider becoming a supporter yourself? 😃

### Changed

- Yarn Spinner's Unity integration now supports Unity 2018.4 LTS and later. (Previously, the minimum version was unspecified, but was actually 2019.2.)
- Fixed a bug that caused the unary minus operator (e.g. `-$foo`) to cause crashes when it's run.
- Unit tests now use test plans, which makes the test cases much more rigorous.
- Methods for working with functions in the `DialogueRunner` class for Yarn Spinner for Unity (thanks to @unknowndevice): 
  - Renamed: `AddFunction` (renamed from `RegisterFunction`)
  - Added: `RemoveFunction`, which removes a function.

### Removed

## [1.0.3] - 2020-02-01

### Added

- The compiler will now reject node titles that contain an invalid character. Invalid characters for node titles are: `[`, `]`, `{`, `}`, `|`, `:`, `#`, `$`, or spaces.
- Added some parser tests for working with node headers.

### Changed

- Fixed a bug where the Dialogue UI component in Unity would not actually send any commands to the 'On Command' event.
- Command handlers will now look for command handlers added via `AddCommandHandler` first (which is faster), followed by commands registered using the `YarnCommand` attribute (which is slower).
- When writing an option (for example, `[[Hello!| Greeting ]]`), any whitespace around the node name (`Greeting`) will be discarded. This fixes a bug where Yarn Spinner would try to go to a node named " ` Greeting ` ", but spaces in node names aren't allowed. (#192)
- Fixed a bug where a null reference exception would be thrown the first time a new Yarn file's Inspector is drawn. (@Schroedingers-Cat)
- Made string table CSVs always be read and written in the Invariant culture. Previously, locale differences would lead to parsing failures. (#197)
- Disabled 'this field is never assigned to' warnings for certain files in the Unity version (they're assigned in the Editor, which the compiler doesn't know about.)

### Removed

## [1.0.2] - 2020-01-23

Bug fixes and small quality-of-life improvements.

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
