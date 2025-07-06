# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

### Updated

- When the `tags:` header on a node is blank, `Node.Tags` now returns an empty collection, instead of a collection containing a single empty string.
- Fixed an issue that would cause testplans with syntax errors to incorrectly pass.
- Fixed an issue where using `visited()` or `visited_count()` would not correctly track visiting node groups.
- Language Server: NodeInfo objects now indicate whether a node contains any jumps to nodes in a different file.
- Language Server: `Microsoft.CodeAnalysis.CSharp` package updated to v4.14.0, fixing command detection in C# files using recent syntax added to C# (eg. `Required Members` from C# 11)

### Removed

## [3.0.2] 2025-06-12

### Added

- Added a new function, `has_any_content`, which takes the name of a node group and returns `true` if any of its member nodes could run.

### Updated

- Fixed a bug where interpolated boolean values inside markup were being incorrectly determined as a string and not a bool.

## [3.0.1] 2025-06-12

### Updated

- Complexity scoring for `when:` headers no longer counts `not` operations. This means that `when: $x` and `when: not $x` have the same score of 1.
- `VirtualMachine.ContentSaliencyStrategy` now defaults to `RandomBestLeastRecentlyViewed`, not `First`
- `NodeDebugInfo.LineInfo` now stores ranges of text, rather than just the start position.

## [3.0.0] 2025-05-16

### Added

- Added support for variadic functions.
- Added support for cancelling compilation.
  - To enable cancelling a compilation, supply a `CancellationToken` to your `CompilationJob` object. You can request that the compilation be cancelled by cancelling the token. For more information, see [Task Cancellation](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation).
- Added `NodeDebugInfo.Range`, which contains the range in which a node appears in the source code.
- Added `NodeDebugInfo.IsImplicit`, which indicates whether the node was created by the compiler (and does not appear in the source code).
- Language Server: Warn on `jump` statements if destination node that does not exist. Offer to create a stub node, or rename to an existing node title.
- The `subtitle` header on a node can be used to control its internal name, for debugging purposes.
- A .NET Standard 2.1 version of the `YarnSpinner` and `YarnSpinner.Compiler` packages have been added.

### Changed

- Improved the performance of the type checker when dealing with large numbers of variable declarations.
- Fixed an issue where `Utility.TagLines` would add `#line:` tags to lines with `#shadow:` tags (which isn't allowed.)
- Language Server: Fixed an error that would crash the language server if a `YarnCommand`- and `YarnFunction`-tagged method was declared inside a nested C# class
- `CompilationJob.VariableDeclarations` has been renamed to `CompilationJob.Declarations`. (`VariableDeclarations` now forwards to `Declarations`, and is marked as deprecated.)
- Language Server: completions now contain node groups, and no longer include the names of individual nodes in a group (which cannot be jumped to directly.)
- `LineParser` now correctly detects the first `:` in a line for the implicit character name
- The `select` marker now converts its value using the invariant culture, not the user's current culture. (This is realistically not a huge change, since values passed to `select` are either integers or non-user-facing strings, but it avoids a potential bug.)
- The language version for `YarnSpinner` and `YarnSpinner.Compiler` has changed from C# 8 to C# 9.
- Language Server: Keyword highlighting for built-in syntax like `jump` and `enum` is now more consistent.

### Removed

- `Yarn.Analysis` types are now removed from the public API.

## [3.0.0-beta2] 2025-03-05

### Added

- The compiler will now warn if additional text is present on the same line before or after commands and other statements.
  - For example, the following code will emit a warning, because the last `>` character is likely a typo: `<<wait 5>>>`
- Added a new method, `Dialogue.GetHeaders`, which returns the collection of headers present on a node.
- Added a new method, `Dialogue.GetHeaderValue`, which returns the value of the specified header on a node.
- Language Server: Nodes that are part of a node group now show their condition complexity as a code lens.
- Added a new method, `Dialogue.HasSalientContent(nodegroup)`, which returns a bool if there is any salient content for the requested nodegroup.
- Added `min(a,b)` and `max(a,b)` to the standard library. We genuinely thought we'd add this earlier.
- Added unwrapped `TryGetProperty` calls to markup, allowing direct access to the type if you know it ahead of time.

### Changed

- Commands are now better at checking to see if the first word is a keyword (e.g. `return`) or a word that just _begins_ with a keyword (`returnToMenu`).
- Fixed an error where the compiler would crash if an error expression resulted in the implicit declaration of a variable (for example, `<<set $x = >>`).
- Fixed an error where whitespace consumption of markup could extend beyond it's immediate younger text sibling.
- Renamed `BestLeastRecentlyViewSalienceStrategy` to be `BestLeastRecentlyViewSaliencyStrategy`.
- Renamed `RandomBestLeastRecentlyViewSalienceStrategy` to be `RandomBestLeastRecentlyViewSaliencyStrategy`.

### Removed

- Removed `GetTagsForNode`. This method is replaced with `GetHeaderValue(nodeName, "tags")`.

## [3.0.0-beta1] 2024-11-29

### Added

#### Enums

Enums have been added to the Yarn language.

Enums are a type of variable that are allowed to be one of a specific set of named values. For example:

```
// Create a new enum called Food.
<<enum Food>>
  <<case Apple>>
  <<case Orange>>
  <<case Pear>>
<<endenum>>

// Declare a new variable with the default value Food.Apple
<<declare $favouriteFood = Food.Apple>>

// You can set $favouriteFood to the 'apple', 'orange' or 'pear'
// cases, but nothing else!
<<set $favouriteFood to Food.Orange>>

// You can use enums in if statements, like any other type of value:
<<if $favouriteFood == Food.Apple>>
  I love apples!
<<endif>>

// You can even skip the name of the enum if Yarn Spinner can 
// figure it out from context!
<<set $favouriteFood = .Orange>>
```

The only valid operators that can be used with enums are `==` (equal to) and `!=` (not equal to).

Enums only support being compared to other values of the same type. 

> For example, if you created a new enum called `Food`, and another enum called `Drink`, you can't compare `Food.Apple` to `Drink.Soda`, because they're different enums.

#### Enum Cases

You declare an enum using the `enum`...`endenum` statement. This can be anywhere in your code. 

Inside your `enum` statement, you put one or more `case` statements, which define one of the values that that enum can be. Each case statement must be unique inside the enum, but you can reuse the same case across different enums.

#### Raw Values

When you add an enum case, you can specify its 'raw value'. This is useful for when you want to use an enum to represent a specific set of numbers or strings. Raw values can be either strings, or numbers. 

Each raw value must be unique inside the enum, but you can reuse the same raw value across different enums. If you specify the raw value of any of an enum's cases, they all must have a raw value. All raw values in an enum must be the same type.

If you don't specify any raw values for an enum, then Yarn Spinner will choose numbers for you as the raw values.

#### Enums and Functions

Functions can receive enums as parameters, as long as the enum's raw value type matches the parameter type. For example, if you have a function `print` that takes a string as a parameter, you can pass any enum to it that uses strings for its raw values; if you have a function `multiplyByTwo` that takes a number as a parameter, you can pass any enum to it that uses numbers for its raw values.

#### Smart Variables

Smart variables have been added to the Yarn language.

A smart variable is one that determines its value at run-time, rather than setting and retrieving a value from storage.

Smart variables give you a simple way to create more complex expressions, and re-use them across your project.

To create a smart variable, declare it using the `declare` statement and provide an expression, rather than a single value:

```
// $player_can_afford_pie is a boolean value that is 'true' 
// when the player has more than 10 money
<<declare $player_can_afford_pie = $player_money > 10>>
```

Smart variables can be accessed anywhere a regular variable would be used:

```
// Run some lines if the player can afford a pie
<<if $player_can_afford_pie>>
  Player: One pie, please.
  PieMaker: Certainly!
<<endif>>
```

#### 'Once' statements

'Once' statements have been added to the language.

A 'once' statement ensures that some content in your dialogue is seen by the player one time only. Uses for this include ensuring that lines where a character introduces themselves don't run multiple times, or barks that should never be run more than a single time (the '[arrow in the knee](https://en.wikipedia.org/wiki/Arrow_in_the_knee)' problem.)

The `once` keyword can be used in two different ways:

##### `once`..`endonce` Statements

A `once`..`endonce` statement allows you to wrap one or more lines (or other kinds of Yarn content) into a block that will only ever run once.

```
<<once>>
  // The guard will introduce herself to the player only once. 
  Guard: Hail, traveller! Well met.
  Guard: I am Alys, the guard!
<<endonce>>
```

`once`..`endonce` statements can be combined with an `if` expression. If the expression evaluates to `false`, the contents of the `once`..`endonce` block will not be run. (The block may run in the future if it's reached again and the expression evaluates to `true`.)

```
<<once if $player_is_adventurer>>
  // The guard knows the player is an adventurer, so say this line, 
  // but only ever once!
  Guard: I used to be an adventurer like you, but then I took an arrow in the knee.
<<endonce>>
```

The `once`..`endonce` statement can also take an `else` block. This block runs if the first part of the `once` statement didn't run.

```
<<once>>
  Guard: Hail, traveller! Well met.
<<else>>
  Guard: Welcome back.
<<endonce>>
```

##### `once` in line conditions

In Yarn Spinner, you can add conditions to the ends of lines, options and line group items to control when they can be presented to the player.

You can use the `once` keyword at the end of a line to make that line only run once. You can also combine this with an `if` expression to make it only run once, and only when the condition passes. If a line with a `once` or `once if` condition has been run before, Yarn Spinner will skip over that line.

```
Guard: Greetings, traveller. <<once>>
Guard: Met some bandits on the road, I see. <<once if $defeated_bandits>>
Guard: Be safe out there.
```

You can use the `once` keyword at the end of an option to make it so that line is only available for selection one time. As with lines, you can also combine this with an `if` expression. An option that has a `once` condition may be shown to the user multiple times as part of a collection of options, but after they select that option, it can't be selected again.

```
-> Where is the castle? <<once>>
-> I must see the king. Where is he? <<once if $needs_to_see_king>>
-> Farewell, friend. <<if $friends_with_guard>>
-> I should go. 
```

> [!NOTE]
> Conditions on options control whether the option is _available to be selected_. Depending on how you've configured your game, this may mean that the option is not shown at all to the player, or that the option is visible but not selectable, or something else user-defined.

Finally, you can use the `once` keyword at the end of a line group item to make it so that it will only ever be run once. As with lines and options, you can combine it with an `if` expression to further control when it may appear.

```
// Scenario: The guard is pursuing the player.
// We'll create some simple, short lines that can run many times without 
// standing out, and some specific lines that we should only ever hear once, 
// because hearing them multiple times would make them stand out.

=> Guard: Halt!
=> Guard: Stop them!
=> Guard: You there! Halt, in the name of the king! <<once>>
=> Guard: Halt, thief! Someone stop them! <<once if $player_stole_treasure>>
```

#### Node groups and 'when' headers

Much like how line groups let you create groups of lines that the system chooses from based on the current game state, node groups let you create groups of _nodes_ that the system chooses from.

You create a node group by creating one or more nodes that all have the same name, _and_ have a `when:` header.

The `when:` header tells Yarn Spinner under what circumstances a particular node can run. For example:

```
title: SpeakToGuard
when: $guard_friendly == true
---
// The guard likes us
Guard: Halt, traveller!
Player: Why, hello there!
Guard: Ah, my friend! You may pass.
===

title: SpeakToGuard
when: $guard_friendly == false
---
// The guard doesn't like us
Guard: Halt, scum!
Guard: None shall pass this point!
===
```

To run this node group, you run the `SpeakToGuard` node. You can do this from within your Yarn scripts, by calling `<<jump SpeakToGuard>>`, or you can do it from within your game (telling your Dialogue Runner to run the node `SpeakToGuard`). Yarn Spinner will then select the most appropriate node to run, using the saliency strategy that you have configured for your game.

You can have as many `when:` headers in a node as you like. If you have more than one, _all_ of their conditions must pass in order for the node to potentially run.

All nodes in a node group must have a `when:` header. It's a compiler error if any of them don't have one.

You can use any of the following kinds of expressions in a `when:` header:

- `when: <boolean expression>` - any expression that evaluates to the values `true` or `false`.
- `when: once` - The node will run precisely one time.
- `when: once if <boolean expression>` - The node will run precisely one time, and only when the expression evaluates to `true`.
- `when: always` - The node may always run.

#### Other Changes

- Standard library functions (e.g. `random`, `round_places`, `dice`) have been moved to the core Yarn Spinner library.
- Added a `format` function to the standard library, this works identical to the C# `string.Format`.
- Added `BuiltInMarkupReplacer` new `IAttributeMarkerProcessor` for the built in replacement markers
- `LineParser` now has `ExpandSubstitutions` method to expand substitutions
- `LineParser` now has `ParseMarkup` method to parse markup in a line
- `LineParser` now can return diagnostics around markup
  - `MarkupDiagnostic` struct encapsulates the diagnostics.
  - `ParseMarkup` has a variant which returns the `MarkupParseResult` and diagnostics, or just the marked up line. 


### Changed

- The Antlr4.Runtime dependency has been upgraded from 4.7.2 to 4.13.1.
- The internal format for storing compiled programs has been updated. Existing Yarn scripts will need to be recompiled in order to work in Yarn Spinner 3.0.
  - Internal jumps inside a node now jump to specific instruction indices, rather than named locations that had to be stored in the file. This change makes compiled Yarn programs smaller.
- The TestPlan system has been improved. (TestPlan is an internal development tool used for unit-testing Yarn Spinner, and is not designed for end-user use.)
    - The TestPlan parser is replaced with an Antlr4-generated parser
    - The TestPlan grammar has been made slightly more consistent:
        - Line, option and command text must now be wrapped in backticks (`)
        - The 'set' and 'stop' commands now no longer take a colon (:)
    - Multiple runs are now supported
    - TestPlan continuation is now no longer driven by the line/option callback system, which makes it easier to reason about.
- Fixed a crash in the compiler that could occur if a node's `title:` header did not have a value.
- Node `visited` and `visited_count` tracking is now handled in the virtual machine when a node is returned from, rather than as a result of compiler-generated code.
- Empty nodes will no longer be included in the compiled output
  - a warning diagnostic will be generated for each empty node
- Fixed a bug where set-referencing inferred value set statements would crash the compiler
- The language server no longer truncates XML documentation comments when it reaches a nested XML node. 
- The constructor for `Yarn.Line` is now public. Previously, it was internal.
- `CompilationResult` now includes a property `UserDefinedTypes`, which contains the types that the user has defined in their script (for example, enums).
- Fixed an issue where the `dice(n)` function would return a value between 0 and n-1, rather than 1 and n.
- .yarnproject files may now specify absolute paths to specific .yarn files.
- `IAttributeMarkerProcessor` replacement method changed from `ReplacementTextForMarker(marker, localeCode)` to `ProcessReplacementMarker(marker,childBuilder,childAttributes,localeCode)`
- Reworked `LineParser` markup handling to better handle reordering and rewriting markers at runtime
  - `nomarkup` attributes are no longer included in the final parsed line.
  - `trimwhitespace` property is now included in the final parsed line.
- `title` header values now follow the same parser rules as other identifiers.

### Removed

- `Dialogue` no longer has line parser responsibilities:
  - removed the `ParseMarkup` method.
  - removed the `ExpandSubstitutions` method.
  - removed `IAttributeMarkerProcessor` conformance.
  - see `LineParser` for replacements.
- `NoMarkupParser` removed due to this functionality now being inside `LineParser`

## [2.5.0] 2024-12-13

### Changed

- `random_range(min, max)` now returns a random integer between `min` and `max`, inclusive. (Previously, it returned a float.)
- Added a new function, `random_range_float` now returns a random float between `min` and `max`, inclusive. (This is the previous behaviour of `random_range`.)

## [2.4.2] 2024-02-24

### Added

- Standard library functions (e.g. `random`, `round_places`, `dice`) have been moved from Yarn Spinner for Unity to the core Yarn Spinner library.
- Added a `format` function to the standard library.
  - This method works identically to the C# `string.Format`, but currently only accepts one parameter.
  - `format("{0}", 123)` will return the string "123".
  - `format("${0:F2}", 0.451)` will return the string "$0.45".
  - For more information on string formatting, see the [.NET documentation on numeric formatting strings](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#standard-format-specifiers).

### Changed

- Updated the schema for .ysls.json files:
  - Commands may no longer specify a return type.
  - Functions _must_ now specify a return type.
  - Changed the definition of 'types' to be an enum of "string", "number", "bool", or "any".
    - Enums in JSON schema are type sensitive, so a warning will be issued for types that have capital letters. To fix these warnings, change your type names in your `.ysls.json` file to be lowercase. (These warnings have no impact on your Yarn script editing experience or runtime behaviour.)
- Empty nodes will no longer be included in the compiled output.
  - A warning diagnostic will be generated for each empty node.
- Fixed a bug where self-referencing inferred value set statements (e.g. `<<set $a = $a + 1>>`, where `$a` is not explicitly declared) would crash the compiler.
- The language server no longer truncates XML documentation comments when it reaches a nested XML node. 
- Updated dependency versions:
  - Google.Protobuf: Moved from `3.15.0` to `3.25.2`.
  - System.Text.Json: Moved from `7.0.2` to `8.0.1`.
  - Microsoft.Extensions.FileSystemGlobbing: Moved from `7.0.0` to `8.0.0`

## [2.4.1] 2024-01-30

- Version 2.4.1 is the first release of the paid version of Yarn Spinner on the [Unity Asset Store](https://assetstore.unity.com/packages/tools/behavior-ai/yarn-spinner-for-unity-267061) and on [itch.io](https://yarnspinner.itch.io). It's identical to v2.4.0.
- Yarn Spinner is and will remain free and open source - we also make it available for purchase as an excellent way to support the team.
- While you're reading, why not consider our [paid add-ons](https://yarnspinner.itch.io), which add some fantastic and easy-to-customise dialogue views?

- Removed the Yarn Spinner v1 to v2 upgrader.
- Removed support for 'raw text' nodes.
  - A 'raw text' node was a node that had 'rawText' in its `tags` header. This indicated to the compiler that the original text of the node should be included in the string table.

## [2.4.0] 2023-11-14

### Added

- Added a new method, `Utility.TagLines`, which will eventually replace the now deprecated `AddTagsToLines` method.
- Added a new method, `Program.LineIDsForNode`, which allows you to get the list of all line IDs in a node.
- Added a new function, `format_invariant`, which formats a number as a string using the invariant culture (rather than the end-user's current culture.)
  - Commands expect all numbers to be formatted using the invariant (i.e. US English) style, with `.` as a decimal point.
  - If a number is inserted into a command, it will by default be formatted for the user's current culture. If that culture formats numbers differently, it can cause problems.
  - `format_invariant` will always format a value in the invariant culture, making it useful for situations where a number needs to be embedded in a command (which expects all numbers to be ), and not shown to the user.
  - You can use `format_invariant` like this:
    ```
    <<set $gold_per_turn = 4.51>>

    // de-DE: 'give_gold 4,51'
    // en-US: 'give_gold 4.51'
    <<give_gold {$gold_per_turn}>>

    // de-DE: 'give_gold 4.51'
    // en-US: 'give_gold 4.51'
    <<give_gold {format_invariant($gold_per_turn)}>>
    ```

### Changed

- Moved Yarn's built-in functions (for example, 'dice' and 'floor') to Yarn Spinner's core library. Previously, they were implemented in the client game code (for example, the Unity runtime.)

#### Language Server

- Fixed a bug in the language server that would cause it to crash when opening a workspace with no root (for example, creating a new window in Visual Studio Code and then creating a Yarn file, without ever saving anything to disk.)
- Fixed an error in the language server that would fail to detect `YarnCommand`- and `YarnFunction`-tagged methods if their attributes were fully-qualified (e.g. `Yarn.Unity.YarnCommand`)
- Renamed VirtualMachine.currentNodeName to VirtualMachine.CurrentNodeName.
- Fixed an issue where workspaces where no Yarn Projects exist on disk would fail to attach Yarn files to the workspace's implicit Yarn Project.
- Improved the code-completion behaviour to provide better filtering when offering command completions, in both jump commands and custom commands.
- Fixed character names being incorrectly recognised when the colon is not part of the line
- Fixed a bug where a missing 'Parameters' field in a .ysls.json file would cause the definitions file to not parse correctly.
- If a workspace that has no .yarnproject files in it is opened, the language server will now look for a .ysls.json file containing command and function definitions. A warning will be reported if more than one file is found.
- The language server now shows a warning if the workspace is opened without a folder.


#### Compiler

- Fixed a crash bug when declaration statements were used without a value (`<<declare $var>>`).
- Fixed a bug where unusual interpolated commands (such as `<<{0}{""}>>`) would resolve to unexpected final values (`<<>>`).
- Fixed an issue that caused the compiler to hang under some circumstances.

#### Utilities

- Flagged the `Utility.AddTagsToLines` method as obsolete.
- Fixed a bug where escaped characters weren't being correctly added back into the file after adding line tags.

### Removed

- Support for merging `Program` objects has been removed.
  - This functionality previously existed because in earlier versions of Yarn Spinner, each individual Yarn script was compiled into a separate Program, which was then merged. Yarn Projects now provide the capabilty to compile multiple Yarn scripts at once, and have done for some time.

## [2.3.1] 2023-07-04

#### Yarn Projects

- Added support for JSON-based Yarn Project files.
  - Yarn Project files contain information that the Yarn Spinner compiler can use to compile multiple Yarn scripts at the same time. Yarn Projects are designed to be used by game engines to identify how Yarn content should be imported into the game.
  - Yarn Project files have the following syntax:
  ```json
  {
    "projectFileVersion": 2,
    "sourceFiles": ["**/*.yarn"],
    "excludeFiles": ["DontInclude.yarn"],
    "baseLanguage": "en",
    "localisation": {
        "en": {
            "assets": "./voiceover/en/"
        },
        "de": {
            "strings": "./de.csv",
            "assets": "../voiceover/de/"
        }
    },
    "definitions": "Functions.ysls.json",
    "compilerOptions": {}
  }
  ```
  - `projectFileVersion` is used to identify which version of the project file format is being used, and is currently required to be the number 2.
  - `sourceFiles` is an array of search paths used to find the Yarn files that should be compiled as part of this project. [Glob patterns](https://en.wikipedia.org/wiki/Glob_(programming)), including globstar, are supported.
  - `excludeFiles` (_optional_) is an array of search paths used to find Yarn files that should _not_ be compiled. The same kinds of patterns as `sourceFiles` are supported.
  - `baseLanguage` is a [IETF BCP 47 language tag](https://en.wikipedia.org/wiki/IETF_language_tag) indicating the language that the source scripts are written in (for example, `en` for English.)
  - `localisation` _(optional)_ is a dictionary containing zero or more objects that describe where locale-specific resources can be found, where the key is a language tag and the value is an object of the following layout:
    - `strings`: The path to a file containing the localised line text for the project. (This is typically, but not required to be, a CSV file.)
    - `assets`: The path to a directory containing the localised assets (for example, voiceover audio) for the project.
  - `definitions` _(optional)_ is the path to a JSON file containing command and function definitions used by the project.
  - `compilerOptions` _(optional)_ is an object containing additional settings used by the Yarn Spinner compiler.

#### Select Content with Line Groups

Yarn Spinner &lt;next&gt; introduces _line groups_. When the dialogue reaches a line group, Yarn Spinner chooses a single item from that group and runs it.

Line groups are especially useful for barks, and for any other situation where the game needs to decide what to show to the player.

>Consider the following example:
>
>```
>// Lines from a soldier who's guarding a castle's draw-bridge.
>=> Halt!
>=> Stop right there!
>=> No entry!
>```
>
>When the dialogue reaches this line group, the soldier will say one of these lines.

Lines in a line group can have _conditions_ that control whether or not they're available to be selected. Conditions can be used to show show lines that depend upon the state of the game.

> For example, imagine that the variable `$is_criminal` represents whether the player has broken the law in the game. The guard can have additional lines added that make sense when this variable is `true`:
>
>```
>=> Halt!
>=> Stop right there!
>=> No entry!
>=> Stop, criminal scum! <<if $is_criminal>>
>=> Halt, you brigand! <<if $is_criminal>>
>=> Thief! Stop right there! <<if $is_criminal>>
>```
>
>If `$is_criminal` is true, then any of these lines may be run, but if `$is_criminal` is false, then one of only the first three lines may be run.

Items in line groups can have 'child' items, which can be any kind of content: lines, options, commands, or any other valid syntax. You add child items to an item in a line group by indenting it.

> For example, the 'wish I'd get a transfer' line in the previous example can be moved to a child line:
> 
> ```
> => Another day, guarding the bridge. 
>   Wish I'd get a transfer.
> ```
> 
> In this example, if the dialogue system selects the line "Another day, guarding the bridge", that line will run, and then the line "Wish I'd get a transfer" will run. 

The way that Yarn Spinner chooses which specific item in a line group to run depends on your game. By default, Yarn Spinner will choose the _first item_ in the line group that passes its condition. However, you can customise this by providing a _saliency strategy_. 'Saliency' means how relevant a piece of content is to the player, and there are several ways to decide which items is the most salient.

Yarn Spinner ships with several saliency strategies to choose from:

* **First**: Chooses the first item. (This strategy is the default, if no other is provided.)
* **Best**: chooses the item that has the most number of variables in its condition; if multiple are available, chooses the first one in the line group.
* **Best least-recently-seen**: Chooses the item that the player has seen the fewest number of times, preferring items that have more variables in their condition; if multiple are available, chooses the first in the line group. Tracks view counts in the game's variable storage.
* **Random best least-recently-seen**: Same as 'best least-recently-seen', but if multiple choices are equally salient, chooses a random one in the group.

You can also create your own custom saliency strategy by creating a C# class that implements the interface `IContentSaliencyStrategy`.

To set a saliency strategy, create an instance of the appropriate saliency strategy class, and set your `Dialogue` object's `ContentSaliencyStrategy` property to it:

```csharp
VariableStorage storage = /* your game's variable storage */
Dialogue dialogue = /* your game's dialogue controller */

dialogue.ContentSaliencyStrategy = new Yarn.Saliency.BestLeastRecentlyViewedSalienceStrategy(storage);
```

### Changed

- Fixed a bug in the language server that caused crashes when code-completion was requested at a position more than 50% of the way through a document.
- The following event handlers on the `Dialogue` class, which were previously required to be set, are now optional and may be set to `null`:
  - `LineHandler`
  - `CommandHandler`
  - `NodeStartHandler`
  - `NodeCompleteHandler`
  - `DialogueCompleteHandler`
  - Note that `OptionsHandler` remains _not_ optional, and is required to be set.
- `Dialogue` now calls `DialogueCompleteHandler` when the `Stop()` method is called.
- VM now nullifies it's state when stopped.

## [2.3.0] 2023-03-06

### Added

- Yarn Programs now store all headers for their nodes.
  - Prior to this change, only the `tags` header was stored.

### Changed

- The Yarn Spinner compiler's indentation tracking has been rewritten to be more consistent in how it works.
  - **🚨 Breaking Change:** `if` statements must now all be at the same level of indentation as their corresponding `else`, `elseif`, and `endif` statements.
    - This was already strongly encouraged for readability, but is now a requirement.
    - If an `if` statement is at a different indentation level to its corresponding statements, a compiler error will now be generated.
    - The lines and other content inside an `if` statement can be indented as much as you like, as long as it's not _less_ indented than the initial `if` statement.
    
      For example, the following code will work:
      ```
      // With indentation
      <<if $something>>
          A line!
      <<else>>
          A different line!
      <<endif>>

      // Without indentation
      <<if $something>>
      A line!
      <<else>>
      A different line!
      <<endif>>
      ```

      The following code will **not** work:

      ```
      // With indentation
      <<if $something>>
        A line!
        <<else>>
      A different line!
      <<endif>>
      ```
  - **🚨 Breaking Change:** Empty lines between options now split up different option groups.
    - Previously, the following code would appear as a single option group (with the options 'A', 'B', 'C', 'D'):
      ```
      -> A
      -> B

      -> C
      -> D
      ```
      In Yarn Spinner 2.3 and above, this will appear as _two_ option groups: one containing the options 'A', 'B', and another containing 'C', 'D'.

      This change was made in response to user reports that the previous behaviour didn't behave the way they expected.

- Node title verification now occurs at declaration time instead of code generation. This means invalid titles will be caught and presented as a problem earlier on, to aid in debugging issues.
- Code completion in the Language Server has been completely rewritten. It is now much less flexible, but *way* more performant. For most situations, the changes will not be noticeable.
- Fixed a crash in the Language Server when encountering declaration statements without a variable.

## [2.2.5] 2023-01-27

### Changed

- Number pluralisation rules have been updated. The rules have now use CLDR version 42.0 (previously, 36.1)
- Merged LanguageServer projects into the core YarnSpinner repository.
- `NodeInfo.PreviewText` no longer removes comments from the preview.
- Migrated tests from xUnit's `Assert` tests to [Fluent Assertions](https://fluentassertions.com).
- Fixed an issue where pluralisation markup (i.e. the `plural` and `ordinal` tags) would not work correctly with country-specific locales (for example "en-AU").

## [2.2.4] 2022-10-31

### Changed

- The compiler will now produce more useful error messages when two or more nodes in a compilation share the same name.

## [2.2.3] 2022-08-28

### Added

- Added a new method, `Utility.DetermineNodeConnections`, that analyses Yarn files and returns a directed graph of node connections.
  - This feature is used in the Language Server to produce reports like voice-over scripts.
- Language Server: New command "yarnspinner.graph" that exports a string which is a graph representation in either mermaid or dot format depending on config.

### Removed

- Removed `null` from the Yarn grammar.
  - `null` was removed from the language in version 2.0.0, but it was kept in the grammar to make it possible to emit special error messages when it was used that explained that the language had changed.

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
