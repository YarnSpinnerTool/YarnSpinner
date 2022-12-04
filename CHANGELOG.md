# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- A document to serve as a roadmap for current and future changes until Yarn 3 is released.

### Changed

- Merged LanguageServer projects into the core YarnSpinner repository.
- `NodeInfo.PreviewText` no longer removes comments from the preview.

### Removed

## [2.2.4] 2022-10-31

### Added

### Changed

- The compiler will now produce more useful error messages when two or more nodes in a compilation share the same name.

### Removed

## [2.2.3] 2022-08-28

### Added

- Added a new method, `Utility.DetermineNodeConnections`, that analyses Yarn files and returns a directed graph of node connections.
  - This feature is used in the Language Server to produce reports like voice-over scripts.
- Language Server: New command "yarnspinner.graph" that exports a string which is a graph representation in either mermaid or dot format depending on config.

## [2.2.2] 2022-07-22

### Changed

- Handling of escape characters is now more consistent in how it approaches the situation of when the first character is the escape character `\`.
- Tagging lines that contain multiwidth characters should no longer create weird invalid split characters in the dialogue.

## [2.2.1] 2022-07-08

### Added

- Added a means to detect and return runs of lines through basic block analysis to the Utils. This is called via the `Yarn.Compiler.Utility.ExtractStringBlocks` function.

### Changed

- Markup attributes may now begin with a digit, letter or underscore. Previously, they were required to begin with a letter or an underscore. This allows the `select` marker to work with numbers: `[select value=1 1=one 2=two 3=three /]`

## [2.2.0] 2022-04-08

### Added

- Added `DeclarationBuilder` and `FunctionTypeBuilder` classes. These classes allow external libraries to construct new `Declaration` and `FunctionType` objects, without having to have access to the internal setters.
- `CompilationResult.DebugInfo` now provides per-instruction positional debug information.
  - This allows users of the `Compiler` class to access positional information for each instruction, which is an important first step for source-level debugging.
- Made `Diagnostic` and `Declaration` serializable, for easier communication with language servers and other utilities.
- The compiler now does a last-line-before-options tagging pass.
  - This will add a `#lastline` tag onto any dialogue line that immediately precedes a block of options.
  - This is intended to used by other parts of the game to modify dialogue view behaviours.
- Language Server: Diagnostics and type information now come from the Yarn Spinner compiler, rather than an independent parsing pass.
- Language Server: Started adding unit tests.

### Changed

- `Declaration` and `Diagnostic` now provide position information via a `Range` object, which specifies the start and end position of the relevant parts of the document.
- Fixed an issue where attempting to access the value of a variable with insufficient context to figure out its type would crash the compiler. (This could happen when you used a variable in a line, like `Variable: {$myVar}` with no other uses of `$myVar`.)
- Fixed an issue where an option condition with no expression (for example: `-> Option one <<if>>`) would crash the compiler.
- The compiler will no longer attempt to generate code if the Yarn script contains errors. (Previously, it was generating code, and then discarding it, but this allows for potential errors and crashes if code-generation is attempted on an invalid parse tree.)
- Typechecker now does partial backwards type inference, allowing for functions and variables to inform the type of the other regardless of them being the l- or r-value in an expression.

### Removed

## [2.1.0] 2022-02-17

### Added

- The `<<jump>>` statement can now take an expression.

```yarn
<<set $myDestination = "Home">>
<<jump {$myDestination}>>
```

- Previously, the `jump` statement required the name of a node. With this change, it can now also take an expression that resolves to the name of a node.
- Jump expressions may be a constant string, a variable, a function call, or any other type of expression.
- These expressions must be wrapped in curly braces (`{` `}`), and must produce a string.

- Automatic visitation tracking.

You can use the `visit` and `visited_count` functions which take in the title of a node and return true of false in the first one, and the number of times visited in the second.
This can be controlled and overriden by the use a header tag `tracking`.
Setting `tracking: always` forces visitation tracking to be enabled even when there are no calls to either function for that node.
Setting `tracking: never` forces no visit tracking regardless of function calls to that node.

### Changed

### Removed

## [2.0.2] 2022-01-08

### Added

### Changed

- Fixed an error when a constant float value inside a marker was parsed and the user's current locale doesn't use a period (`.`) as the decimal separator.

### Removed

## [2.0.1] 2021-12-23

### Added

- The v1 to v2 language upgrader now renames node names that have a period (`.`) in their names to use underscores (`_`) instead. Jumps and options are also updated to use these new names.

### Changed

- Fixed a crash in the compiler when producing an error message about an undeclared function.
- Fixed an error when a constant float value (such as in a `<<declare>>` statement) was parsed and the user's current locale doesn't use a period (`.`) as the decimal separator.

## [2.0.0] 2021-12-20

### Added

### Changed

- Fixed an issue where line tags could be added at an incorrect place in a line, if that line contained a condition.

### Removed

## [2.0.0-rc1] 2021-12-13

v2.0.0-rc1 contains no user-facing features or bug fixes; it exists to be in sync with the corresponding v2.0.0-rc1 tag for Yarn Spinner for Unity.

## [2.0.0-beta6] 2021-10-23

### Added

- The Compiler will no longer throw a `ParseException`, `TypeException` or `CompilerException` when an error is encountered during compilation. Instead, `CompilationResult.Diagnostics` contains a collection of `Diagnostic` objects, which represent errors, warnings, or other diagnostic information related to the compiled program.
  - This change was implemented so that if multiple problems can be detected in a program, they can all be reported at once, rather than the compiler stopping at the first one.
  - This also allows the compiler to issue non-fatal diagnostic messages, like warnings, that do not prevent the script from being compiled, but might indicate a problem with the code.
  - Exceptions will continue to be thrown if the compiler encounters an internal error (in other words, if Yarn Spinner itself has a bug.)
- If an error is encountered during compilation, `CompilationResult.Program` will be `null`.
- This change means that compilation failures will not cause  `Compiler.Compile()` to throw an exception; code that was previously using a `try...catch` to detect problems will need to be rewritten to check the `CompilationResult.Diagnostics` property to find the actual problem.

### Changed

- Made the lexer not use semantic predicates when lexing the TEXT rule, which reduces the amount of C# code present in the grammar file.
- Markup can now be escaped, using the `\` character:

```
\[b\]hello\[/b\]
// will appear to the user as "[b]hello[/b]", and will not 
// be treated as markup
```
- `Dialogue.SetSelectedOption` can now be called within the options handler itself. 
  - If you do this, the `Dialogue` will continue executing after the options handler returns, and you do not need to call `Continue`.

- The compiler now generates better error messages for syntax errors. For example, given the following code (note the lack of an `<<endif>>` at the end):

```yarn
<<if $has_key>>
  Guard: You found the key! Let me unlock the door.
```

The compiler will produce the following error message:

```
Expected an <<endif>> to match the <<if>> statement on line 1
```

- The compiler's new error messages now also report additional information about the context of a syntax error. For example, given the following code:

```yarn
<<if hasCompletedObjective("find_key" >>
  // error! we forgot to add an ')'!
<<endif>>
```

The compiler will produce the following error message:

```
Unexpected ">>" while reading a function call
```

- `VirtualMachine.executionState` has been renamed to `VirtualMachine.CurrentExecutionState`.

- It is now a compiler error if the same line ID is used on more than one line.

- Dialogue.VariableStorage is now public.

### Removed

- The ParseException, TypeException and CompilerException classes have been removed.

## [2.0.0-beta5] 2021-08-17

### Added

#### Variable declarations are now optional
- If a variable is not declared (i.e. it doesn't have a `<<declare>>` statement), the compiler will now attempt to infer its declaration.
- When a variable doesn't have a declaration, the compiler will try to figure out the type based on how the variable is being used. It will always try to figure out the _single_ type that the variable _must_ be; if it's ambiguous, or no information is available at all, it will report an error, and you will have to add a declaration.


#### Variable declaration descriptions now use comments
- Declarations now have their descriptions set using a triple-slash (`///`) comment:

```
/// The number of coins the player has
<<declare $coins = 0>>
```

- These documentation comments can be before a declaration, or on the same line as a declaration:

```
<<declare $player_likes_dogs = true>> /// Whether the player likes dogs or not
```

- Multiple-line documentation comments are also supported:

```
/// Whether these are the droids that the 
/// guards are looking for.
<<declare $are_the_droids_we're_looking_for = false>>
```

#### A new type system has been added.

- The type-checking system in Yarn Spinner now supports types with supertypes and methods. This change has no significant impact on users writing Yarn scripts, but it enables the development of more advanced language features. 
  - The main impact on users of this library (such as, for example, Yarn Spinner for Unity) is that the `Yarn.Type` enumeration has been removed, and is now replaced with the `Yarn.IType` interface and the `BuiltinTypes` class.
  - The type checker no longer hard-codes which operations can be run on which types; this decision is now determined by the types themselves.

### Changed

- Variable declaration upgrader now generates .yarnproject files, not .yarnprogram files.
- Line tagger now adds line tags before any `//` comment in the line.
- Dialogue: `LogErrorMessage` and `LogDebugMessage` now perform null-checks before being invoked.
- `Utility.GenerateYarnFileWithDeclarations` now generates files that use triple-slash (`///`) comments.
- Fixed a bug where expressions inside an `if` statement or `elseif` statement would not be type-checked.
- The keywords `enum`, `endenum` and `case` are now reserved.
- The type-conversion functions, `string`, `number` and `bool`, are no longer built-in special-case functions; they are now regular built-in functions that take a value of `Any` type.

### Removed

- In previous betas, variable descriptions were done by adding a string. This has been removed:
  
```
// This will no longer work:
<<declare $coins = 0 "The number of coins the player has">>
```

## [2.0.0-beta4] 

### Added

- Characters can now be escaped in lines and options.
  - The `\` character can be used to write characters that the parser would otherwise use.
  - The following characters can be escaped: `{` `}` `<` `>` `#` `/` `\`
    - The `/` and `<` characters don't usually need to be escaped if they're appearing on their own (they're only meaningful when they appear in pairs), but this allows you to escape things like commands and comments. 
- Identifiers now support a wider range of characters, including most multilingual letters and numbers, as well as symbols and emoji.

### Changed

- Made line conditions control the `IsAvailable` flag on options that are sent to the game. 
- This change was made in order to allow games to conditionally present, but disallow, options that the player can't choose. For example, consider the following script:

```
TD-110: Let me see your identification.
-> Of course... um totally not General Kenobi and the son of Darth Vader.
    Luke: Wait, what?!
    TD-110: Promotion Time!
-> You don't need to see his identification. <<if $learnt_mind_trick is true>>
    TD-110: We don't need to see his identification.
```

- If the variable `$learnt_mind_trick` is false, a game may want to show the option but not allow the player to select it (i.e., show that this option could have been chosen if they'd learned how to do a mind trick.)
- In previous versions of Yarn Spinner, if a line condition failed, the entire option was not delivered to the game. With this change, all options are delivered, and the `OptionSet.Option.IsAvailable` variable contains `false` if the condition was not met, and `true` if it was (or was not present.)
- It's entirely up to the game to decide what to do with this information. To re-create the behaviour from previous Yarn Spinner versions, simply don't show any options whose `IsAvailable` value is `false`.

- Fixed a crash in `LineParser` if a null input was provided to it.
- Fixed a crash in `FormatFunctionUpgrader` (which upgrades v1 Yarn scripts to v2) if an invalid format format function was encountered.

### Removed

## [2.0.0-beta2] 2021-01-14

### Added

- The `[[Destination]]` and `[[Option|Destination]]` syntax has been removed from the language.
  - This syntax was inherited from the original Yarn language, which itself inherited it from Twine. 
  - We removed it for four reasons: 
    - it conflated jumps and options, which are very different operations, with too-similar syntax; 
    - the Option-destination syntax for declaring options involved the management of non-obvious state (that is, if an option statement was inside an `if` branch that was never executed, it was not presented, and the runtime needed to keep track of that);
    - it was not obvious that options accumulated and were only presented at the end of the node;
    - finally, shortcut options provide a cleaner way to present the same behaviour.
  - We have added a `<<jump Destination>>` command, which replaces the `[[Destination]]` jump syntax.
  - No change to the bytecode is made here; these changes only affect the compiler.
  - Instead of using ``[[Option|Destination]]`` syntax, use shortcut options instead. For example:

```
// Before
Kim: You want a bagel?
[[Yes, please!|GiveBagel]]
[[No, thanks!|DontWantBagel]]

// After
Kim: You want a bagel?
-> Yes, please!
  <<jump GiveBagel>>
-> No, thanks!
  <<jump DontWantBagel>>
```

- An automatic upgrader has been added that attempts to determine the types of variables in Yarn Spinner 1.0, and generates `<<declare>>` statements for variables.
  - This upgrader infers the type of a variable based on the values that are assigned to it, and the values of expressions that it participates in.
  - If the upgrader cannot determine the type of a variable, it generates a declaration of the form `<<declare $variable_name as undefined>>`. The word `undefined` is not a valid type in Yarn Spinner, which means that these declarations will cause an error in compilation (which is a signal to the developer that the script needs to be manually updated.)

 - For example: given the following script:

```   
<<set $const_string = "foo">>
<<set $const_number = 2>>
<<set $const_bool = true>>
```
    
- The upgrader will generate the following variable declarations:
```
    <<declare $const_string = "" as string>>
    <<declare $const_number = 0 as number>>
    <<declare $const_bool = false as bool>>
```
    
The upgrader is able to make use of type even when it appears later in the program, and is
able to make inferences about type using indirect information.
    
```
// These variables are participating in expressions that include
// variables we've derived the type for earlier in this program, so they
// will be bound to that type
{$derived_expr_const_string + $const_string}
{$derived_expr_const_number + $const_number}
{$derived_expr_const_bool && $const_bool}

// These variables are participating in expressions that include
// variables that we define a type for later in this program. They will
// also be bound to that type.
{$derived_expr_const_string_late + $const_string_late}
{$derived_expr_const_number_late + $const_number_late}
{$derived_expr_const_bool_late && $const_bool_late}

<<set $const_string_late = "yes">>
<<set $const_number_late = 1>>
<<set $const_bool_late = true>>
```

- The upgrader will also make in-line changes to any if or elseif statements where the expression is determined to use a number rather than a bool will be rewritten so that the expression evaluates to a bool:

``` 
// Define some variables whose type is known before the expressions are
// hit
<<set $some_num_var = 1>>
<<set $some_other_num_var = 1>>

// This will be converted to a bool expression
<<if $some_num_var>>
<<elseif $some_other_num_var>>
<<endif>>
```

Will be rewritten to:

```
<<if $some_num_var != 0>>
<<elseif $some_other_num_var != 0>>
<<endif>>
```

### Changed

- The internal structure of the LanguageUpgrader system has been updated to make it easier to add future upgrade passes.

### Removed

## [2.0.0-beta1] 2020-10-20

### Added
- Version 2 of the Yarn language requires variables to be declared in order to use them. It's now an error to set or get a value from a variable that isn't declared.
  - Variables must always have a defined type, and aren't allowed to change type. This means, for example, that you can't store a string inside a variable that was declared as a number.
  - Variables also have a default value. As a result, variables are never allowed to be `null`.
  - Variable declarations can be in any part of a Yarn script. As long as they're somewhere in the file, they'll be used.
  - Variable declarations don't have to be in the same file as where they're used. If a script has a variable declaration, other scripts compiled with it can use the variable.
  - To declare a variable in a script, use the following syntax:
  
```
<<declare $variable_name = "hello">> // declares a string
<<declare $variable_name = 123>> // declares a number
<<declare $variable_name = true>> // declares a boolean
```

- Added substitution support to Dialogue (previously, the game client had to do it)
- Added support for markup.
- Added an EditorConfig file to assist future contributions in following the .NET coding style (@Schroedingers-Cat)
- Added Dialogue.prepareForLinesHandler, a delegate that is called when the Dialogue anticipates running certain lines; games can use this to pre-load content or take other actions to prepare to run lines.
  - Yarn Spinner will check the types of the delegate you provide. At present, parameters must be either ints, floats, doubles, bools, strings, or `Yarn.Value`s.
- Added a new command, `<<jump>>`, which immediately jumps to a new node. It takes one parameter: the name of the node to jump to.

### Changed

- `Library.RegisterFunction` no longer works with the `Function` and `ReturningFunction` classes, which have been removed. Instead, you provide a `Func` directly, which can take multiple individual parameters, rather than a single `Value[]` parameter.
- The `LineHandler`, `CommandHandler`, and `NodeCompleteHandler` callbacks, used by the `Dialogue` class, no longer return a value that indicates whether the `Dialogue` should pause execution. Instead, the `Dialogue` will now *always* pause execution, which can be resumed by calling `Dialogue.Continue()`. (This method may be called from inside the line handler or command handler, or at any point after these handlers return.)
- The `Compiler` class no longer compiles Yarn scripts using the `CompileFile` and `CompileString` methods. Instead, the `Compile` method accepts a `CompilationJob` struct that describes the work to do, and returns a `CompilationResult` struct containing the result. This method allows for the compilation of multiple files into a single program, as well as supplying variable and function declarations.
- The `Compiler` class also supports doing only a partial compilation, returning only variable declarations or string table entries.
- Yarn scripts are now all compiled into a single `YarnProgram`. This improves compilation performance, ensures that scripts don't have multiple nodes with the same name, and ensures that scripts are able to make use of variables declared in other scripts.
- Shortcut options have been renamed to "options".

### Removed

- `[[Option]]` syntax has been removed.
  - In previous versions of the Yarn language, there were two ways of presenting options to the player: "regular" options (`[[Displayed text|DestinationName]]`), and shortcut options (`-> Displayed Text`), with shortcut options being displayed immediately, and regular options accumulating and being presented at the end of the node.
  - In Yarn Spinner 2.0, the "regular" option syntax has been removed; when you want to show options to the player, use the "shortcut option" syntax.  
  - The previous, related syntax for jumping to another node, (`[[DestinationNode]]`), has also been removed, and has been replaced with the `<<jump>>` command.
- Functions registered with the `Library` class can no longer accept an unlimited number of parametes.

## [1.2.0] 2020-05-04

### Added

- Added Nuget package definitions for [YarnSpinner](http://nuget.org/packages/YarnSpinner/) and [YarnSpinner.Compiler](http://nuget.org/packages/YarnSpinner.Compiler/).

### Changed

- Parse errors no longer show debugging information in non-debug builds.

### Removed

## [1.2.0-beta1] 2020-05-28

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

## [1.1.0] - 2020-04-01

Final release of v1.1.0.

## [1.1.0-beta3]

### Added

### Changed

- Fixed a bug that caused `<<else>>` to be incorrectly parsed as a command, not an `else` statement, which meant that flow control didn't work correctly.

## [1.0.0-beta2]

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
