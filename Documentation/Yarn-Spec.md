# Yarn Spec

This document describes the specification of the Yarn Script format version 2 and guidance and rules for implementing programs who wish to process Yarn scripts.

## Introduction

Yarn is a language for representing branching and linear interactive fiction in games.
Yarn was created for the game Night in the Woods and was inspired by the Twine language.
Its focus is on flexibility and writer ease, the goal of the language is to have clear syntax when necessary but to otherwise be as close to just writing text as possible.
For writers there should be minimal friction in creating the language and for programmers minimal annoyance when interfacing with it.

During development of Night in the Woods Yarn Spinner was created as an open source side project and had no affiliation to Night in the Woods at the time.
Yarn Spinner could read and understand Yarn files but had several advantages over the Yarn interpreter in Night in the Woods so the decision was made to use Yarn Spinner for the game.
This made Yarn Spinner the de facto Yarn interpreter and had the side effect of giving it control over the specification of the language.
Yarn essentially became and was always defined in terms of what Yarn Spinner could understand.

As Night in the Woods was developed additional features and work was done on Yarn Spinner for the game the set of features and syntax exploded and became difficult to understand or reimplement.
Post Night in the Woods Yarn Spinner continued as its own thing with version 0.9 being released which was mostly a polish of the version used in Night in the Woods.
Later version 1.0 of Yarn Spinner, and as such the language, came out with improved syntax and was the first significant release that wasn't tied to Night in the Woods.
Neither Yarn Spinner 0.9 or 1.0 however came with complete specifications of the Yarn language and had a great deal of legacy elements.

A concerted effort was made to clean up the Yarn language in 2020 for Yarn Spinner 2.0 into something hopefully more understandable but also more flexible.
A component of this is the creation of this specification guide so implementations beyond Yarn Spinner can have something to base decisions on that isn't "do what Yarn Spinner does".

Despite this being the first version of the Yarn language to ever exist it is version 2 of the Yarn language.
This is because despite Yarn and Yarn Spinner being separate elements attempting to not stay, at least initially, in version lock with the de facto implementation is just asking for trouble, hence version 2 of Yarn.

### Coverage

This document covers the elements of a Yarn script that are required to be considered a valid yarn script as well as the rules necessary for an implementing program to be able to conform to this spec.

An *implementing program* is a program that accepts yarn files as inputs and understands them based on the rules in this document.
This document does not cover how a yarn file is to be transformed or handled internally by an implementing program.

This document does note when behaviours are *unspecified*.
The implementing program may choose how to handle any unspecified behaviour, often unspecified behaviours have no good solution and should result in an error.
An example of unspecified behaviour are required tags on Nodes.
Only the `Title` tag is required, an implementation may choose to make other tags required or banned.

### Assumptions

There is one large assumption in this document and about the language itself.
That is it that it is intended to be embedded into a larger game project.

As such details like how lines are viewed, how selections of options are made, how directions get used, or how functions are handled are all assumed to be passed off onto the larger game to handle.
This is not technically necessary but does explain what would otherwise be gaps in the language.
If your project does not have a game to offload certain tasks to these elements will have to be handled by the implementing program itself.

### Reading This Specification

`monofont` terms are to be taken as literals.
As an example the `Title` tag would be written as a string literal `"Title"` in C#.

*italics* terms are terms which are reused throughout the document.
They are presented in italics the first time they are defined.

_Must_ is a hard requirement.
_Should_ is a recommendation, albeit a strong one.

_Errors_ are mentioned multiple times and represent situations that are unrecoverable.
Errors are intended to allow the implementing program to let the user or other parts of the game know that an unrecoverable situation has occured.
The implementing program must abort progress on the Yarn after creating an error.
The precise handling of errors will be specific to the implementing program but should use whatever error mechanisms exist already, for example Yarn Spinner throws normal C# exceptions for its errors.

Dates, spelling, and numbers are to be in Australian English.

### Modifying This Specification

Once this specification is complete it is set and unchanging.
Modifications beyond clarifications won't be allowed.
To make changes to the language specification a new version of the language will need to be created.

This is to ensure implementing programs can conform to a language version and not have to worry about it changing after the fact.

## File Format

Yarn files must be a UTF-8 text file without BOM set.
File extension should be `.yarn`.

### Lines

The *line* is the common unit that comprises all elements in a yarn file.
A line is a series of characters terminated by the *new line* symbol.
The new line symbol should be the `\n` character.
The new line must be the same throughout the project regardless of the chosen new line symbol.

The following Yarn file contains four lines (in order); one header tag line, one header delimiter line, one body dialogue line, one body delimiter line.
```yarn
Title:Start
---
This is some text
===
```

### Whitespace

*Whitespace* is any non-visible character with a width greater than 0.
Common whitespace encountered include the space and the tab.

Whitespace for lines of dialogue plays no role but has significant syntactic important for Options.

### Project

The yarn *project* is all yarn files that are intended to be associated with one another.
While there is nothing stopping a writer from placing all nodes into one big file (or even making one giant node) it is common to break them up over multiple files.
The project is all of these files collected and processed by the implementing program together.

### Comments

A comment is a line that starts with the `//` symbol.
All text from the start of the comment to the end of the line must be ignored.
A comment starting in the middle of another line ends that line at the point the `//` symbol is encountered.
That line is assumed to have finished at that point as if the comment was not there.
Comments must not impact the rest of the lines or have any impact on the resulting yarn program.
 
### Identifiers

Throughout various points in this document identifiers are mentioned, the rules for these are shared across all stages of the Yarn project.

An *identifier* is any of the following symbols: an upper or lowercase letter A to Z, an underscore (`_`), a noncombining alphanumeric Unicode character in the Basic Multilingual Plane, or a character outside the Basic Multilingual Plane that isn't in the Private Use Area.
After the first character digits, a period (`.`), and combining Unicode characters are also allowed.
But not another `$` symbol.
The minimum and maximum length of identifiers is unspecified.

## Yarn Structure

A Yarn script file is yarn file that contains one or more nodes and zero or more file tags.

### File Tags

_File tags_ are file level metadata that is relevant for all nodes in the file.
A common use for file tags is for versioning the file.
File tags must go at the start of a file before any nodes begin.
File tags must have the `#` symbol at the start of them and then contain all text up until the end of the line.

### Nodes

A *node* is the single story element of a yarn file.
Nodes are the story structural building blocks for yarn.
Nodes are designed to contain pieces of a story and then have these story pieces linked together.
This is not a requirement, everything could be done in a single node, this would just be unwieldy.
A node must be comprised of a single header and a single body in that order.

### Headers

A *header* is comprised of one or more header tags.
The header is finished when encountering a line that only contains the *header delimiter* `---`.
After encountering the header delimiter the body of the node is entered.

#### Header Tags

A *header tag* is a line broken up into three components, in order; the tag name, the separator, and the tag text.
The *tag name* is an identifier.
The *tag separator* is the character `:`.
The *tag text* is all text up until the end of line.
Header tags are commonly used as node specific metadata but using them in this manner is not required, beyond the title tag.

The amount of allowed whitespace between the tag name, the seperator, and the tag text is unspecified.
An example of a header tag is the title tag: `Title:start`.

Every node must have a title tag.
Required or banned header tags beyond title are unspecified.
The order of header tags is unspecified.

#### Title Tag

The *title tag* is a specific header tag that uniquely identifies the node.
The tag name for the title tag must be `Title`.
The tag text for the title tag must be unique within the file.
The tag text for the title tag should be unique within the project.
The tag text must follow the rules of identifiers.

The behaviour of the program when a title tag's text is not unique across the project is unspecified.
The program should flag this as an error.

### Body

A body is comprised of multiple statements.
A *statement* is a line that is one of the following:

- dialogue
- commands
- options

A statement may have optional hashtags.
A body must have at least one statement.

The body ends when encountering a line that contains the *body delimter* `===`.
The body delimiter ends both the current node and the body of that node.
The end of file must not be used in place of a body delimiter.

#### Hashtags

*Hashtags* are metadata associated with the statement they are a part of.
Hashtags must go at the end of the statement.
The other components of the statement must end at the hashtag, the hashtag operates effectively as the newline terminator for the statement.

A hashtag starts with the `#` symbol and contain any text up to the newline or another hashtag.
`#lineID:a10be2` is an example of a hashtag.
Multiple hashtags can exist on a single line.
`#lineID:a10be2 #return` is an example of multiple hashtags on a line.
`General Kenobi: Why hello there #lineID:a10be2 #return` is an example of a line of dialogue with multiple hashtags.

### Dialogue Statement

A dialogue statement is a statement that represents a single line of text in the yarn story.
In most cases dialogue will be the bulk of the content of a node's body.
Dialogue lines can be interpolated dialogue or raw dialogue.

An *interpolated dialogue* is one where there are in-line expressions in the line.
Expressions are encapsulated within the `{` and `}` symbols and it is the presence of these symbols that determine if a line is an interpolated one or not.
The expression inside the `{}` symbols must be a valid expression.
The result of the expression must be inserted into the dialogue.
Other than replacing expressions dialogue statments must not be modified by the implementing program, and provided to the game as written.

A *raw dialogue* is a dialogue statement where there are no expressions.

A dialogue statement can contain any characters except for the `#` character.

`{$name}, you are a bold one.` is an example of an interpolated dialogue.
`General Kenobi, you are a bold one.` is an example of a raw dialogue.

When resolving ambiguity of statements inside the body the dialogue statement must be considered the lowest priority by the implementing program.
For example `<<Fred Move Left>>` could be read as a command or a dialogue statement, it must be considered a command by the implementing program.

#### Escaping Text

There are going to be times in dialogue that the writer will need to use symbols that are reserved.
To use reserved symbols in dialogue preface any reserved symbol with the escape symbol `\`, this allows the following symbol to escape being understood as a reserved character.
Any character following the escape must be presented in the dialogue as-is and must not be parsed as a special character.
As an example `\{$name\}, you are a bold one.` would be presented as `{$name}, you are a bold one.` to the game.

Escaping text must be supported in both normal and interpolated dialogue lines as well as in the dialogue component of options.

### Commands

Commands are special statements that have no specific output to be shown but are used for passing messages and directions to other parts of the program and to control the flow of the story.

The possible types of commands are:

- directions
- jump
- stop
- set
- declare
- flow control

All commands must start with the `<<` symbol and end with the `>>` symbol.
Additional required command are unspecified.

#### Directions

_Directions_ are commands for sending messages from the yarn to the rest of the program.
Implementing programs must not modify the flow of the yarn based on the direction.

Directions can have any text except for the `#`, `{`, or `}` symbols inside of them.

Directions can also have expressions inside of them, however as with dialogue these must be encapsulated by using the `{` and `}` symbols.
Any expressions inside of a directions command without being encapsulated must be ignored and treated instead as regular text.

```yarn
<<Fred Move Left 2>>
<<Unlock Achievement MetSteve>>
<<Log {$playerName} Died>>
```
are examples of directions.

#### Jump

The _jump_ command is how a yarn program can move from one node to another.
The jump has two components: the keyword and destination.
The _keyword_ is the text `jump` and comes first in the command.

The _destination_ is the name of the node to move to.
The destination may be any text but must map to the `Title` of a node in the project.
The destination text may be created using the result of an expression, however this must be wrapped inside `{` `}` symbols.
The expression must resolve to a string value and must be a string that matches a node title in the project.

The behaviour of an implementing program is unspecified when asked to jump to a destination that doesn't match a title in the project.
The implementing program should flag this as an error.

Once the jump command has been completed the current node must be exited immediately, this means any dialogue, options or commands below the jump are to be ignored.
From that point on the destination nodes contents must instead be run.

`<<jump nodeName>>` is an example of a jump command, `<<jump {$chosenMurderer}>>` is an example of a jump command using an expression to determine the destination node.

#### Stop

The _stop_ command is for halting all progress on the project.
Once the stop command is reached all processing on the project must halt, no additional nodes are to be loaded and run, no additional dialogue or commands are to processed.
The stop command has only one component, the _keyword_ `stop`.
The stop command should reset any variable or internal state back to their initial states.

`<<stop>>` is the example of the stop command.

#### Set

The _set_ command allows variables to be given values.
The set command has four components: the keyword, the variable, the operator and the value and must be presented in that order.

The _keyword_ is the text `set`.
The _variable_ is the name of the variable which is to have its value changed.
The _operator_ must be the text `to` or `=`.
The _expression_ is any expression, unlike other uses of expressions this one must not be wrapped inside the `{` and `}` symbols.

The following is an example of two set commands:
```yarn
<<set $name to "General Kenobi">>
<<set $boldness to $boldness + 1>>
```

The set command must follow all the rules for variable naming and expressions.
The set command must not allow setting a variable to an expression whose value is different from the type of that variable.

#### Declare

Variables in Yarn should be declared to let the implementing program know the type of values they hold.
The intent of this is to allow the implementing program to set up memory and to provide guidance as to the usage of a variable directly from the writer.
The declare command has four components: the keyword, the variable, the operator and the value, and must be presented in that order.

The _keyword_ is the text `declare`.
The _variable_ is the name of the variable which is to have its value changed.
The _operator_ must be the text `=` or `to`.
The _expression_ is any expression, unlike other uses of expressions this one must not be wrapped inside the `{` and `}` symbols.

The resulting value of the expression is used determine what type the value has been declared as, so if expression results in a boolean for example then the variable is declared as a boolean.

The following is an example of two declaration commands:
```yarn
<<declare $name = "General Kenobi">>
<<declare $boldness = 1>>
```

In these examples we have declared two new variables `$name` and `$boldness`.
The value of the expression is used determine what type the value is to be declared as, so in the above examples `$name` is typed as a string because the expression value of `"General Kenobi"` is a string.

The implementing program must not allow the variable declared to ever have a value set which is not of the declared type.
If this does occur the implementing program must flag this as an error.
The handling of encountering variables which have not been declared is unspecified but should generate an error.

##### Explicit Typing

It is assumed that most of the time a variable's type will be determined implicitly via the initial expression, however the type can also be explicitly set.
Syntactically this works identically to the implicit type declaration with two additional elements at the end of the command, the `as` keyword and a type.
The type of the expression must match one of the suported types keywords:

- `Number` for Numbers
- `Bool` for Booleans
- `String` for Strings

`<<declare $name = "General Kenobi" as String>>` is an example of an explicitly typed declaration.
Explicitly typed declarations will most likely be used when getting intial values from functions where the type is undefined but they can be used anywhere.
The default value's type given in a an explictly typed declaration must match the type, for example `<<declare $name = "General Kenobi" as Number>>` is an invalid declaration because `General Kenobi` isn't a `Number`.

If additional types are in use by the implementing program the keywords for their explicit definition are unspecified, but they must be consistent across all declarations.

### Flow control

Flow control is a collection of commands that allow the writer to control the flow of the story.
There are four commands which work in conjunction to support flow control:

- if
- else
- elseif
- endif

The order of these commands is always the same and must be followed:

1. if
1. elseif
1. else
1. endif

The if and endif must be present, the elseif and else must be optional.
While each of these commands are their own statement they should be considered to be part of a larger flow control statement which spans multiple lines.
Each of these, except the `endif`, have an attached block.

The following is an example of flow control, the dialogue line to be shown will depend on the value of `$var`.
If `$var` is `1`, the line `if-scope` will be presented, if it is `2` then the `elseif-scope` line will be shown.
If neither of those are the case then the `else-scope` line will be shown.
```yarn
<<if $var == 1>>
    if-scope
<<elseif $var == 2>>
    elseif-scope
<<else>>
    else-scope
<<endif>>
```

#### if

The _if_ command is the opening command of flow control and is broken up into two parts, the keyword and the expression and must be in that order.
The _keyword_ is the text `if`.
The _expression_ is an expression.
The expression must resolve to a boolean.

`<<if $boldness > 1>>` is an example of an if command, `<<if 1>>` is an example of an invalid if, it is invalid because the expression does not resolve to a boolean.

#### elseif

The _elseif_ command is an optional component of flow control and allows for additional flow to be expressed.
The command works in a fashion very similar to the if command.
The command is broken up into two parts, the keyword and the expression and must be presented in that order.
The _keyword_ is the text `elseif`.
The _expression_ is an expression.
The expression must resolve to a boolean.

The elseif will run only if the `if` component, and any other `elseif`'s evaluated to false, and if its own expression evaluates to true.

The minimum mumber of required elseif commands must be zero.
The maximum number of allowed elseif commands is unspecified but should be greater than zero.
An elseif command must not exist without an if command and must go after the if command.

#### else

The _else_ command is an optional component of flow control and allows for additional flow to be expressed.
The command only has a single component, the keyword `else`.

There must only be a single else command (if any) per flow control.
The else command must go after the if and any elseif commands.
The else must not exist without an if command.

The else's block will run only if the `if` and any `elseif` components all evaluated to false.

The example of the else command is `<<else>>`.

#### endif

The _endif_ command is the final element of flow control and is comprised solely of the keyword `endif`.
The endif must be present whenever there is flow control and must go after the if and any elseif or else commands.
The endif exists to allow the implementing program know when the scope of the other elements in the flow control has ended.
`<<endif>>` is the example of the endif command.

#### Scope and Blocks

For the flow control to be useful there needs to be yarn statements which are run only when their appropriate expression evaluates to true.
Flow control allows for blocks of statements to be scoped to their commands.
A _block_ is a collection of statements that are scoped to a particular part of the flow control.

The _scope_ of a block is determined by the flow control commands and associates each block with a command.
The if, elseif, and else commands all have a block associated with them.
The block of statements for a command start from the first statement after the command up until the next command in the flow control.
When dealing with nested flow control the deepest set of flow control commands are to be the ones that can first assume another command closes their scope.

While it is common for writers to indent their blocks relative to their scope it must not be used by the implementing program to determine scope.

#### Handling 

The implementing program must process all statements within the active blocks scope.
The _active block_ is the block of yarn who's command expression evaluates to `true`.
The block associated with the else command, if present, must only be determined as the active block if all other blocks expressions evaluate to false.

An implementing program must not process any statements inside a block that is not the active block.
An implementing program must only have, at most, one active block.
If no blocks expression evaluates to true then no block must be processed.

#### Ambiguity

Because the flow control commands allow for potentially multiple commands and their blocks to be the true one, the implementing program must select them in a top down approach when there is conflicting flow.
For example take the following flow control:

```yarn
<<if false>>
    if-scope
<<elseif true>>
    elseif-1-scope
<<elseif true>>
    elseif-2-scope
<<else>>
    else-scope
<<endif>>
```

Both of the elseif commands expressions evaluate to true, so either ones attached block could be run and seen to be correct.
However because one is above the other the block with `elseif-1-scope` dialogue inside would be the selected one.
The implementing program should attempt to identify these scenarios however and alert the writer.

### Options

_Options_ are the means by which Yarn can present dialogue choices to the game and much as with flow control are an element that spans multiple lines.
Options are comprised of one or more option lines.
An _option line_ represents a single choice in an option, and are comprised of three parts: the keyword, the dialogue, the conditional in that order.

The _keyword_ is how the implementing program can tell a line is part of an option instead of dialogue and is the symbol `->`.
There must be at least one whitespace between the keyword and the next element, the dialogue.
The _dialogue_ is a normal line of dialogue following all rules associated with that.

As the intention of options is to provide choice to the player when options are encountered the implementing program must halt further progress through the node until an option has been selected.
Each option must be provided in the order they are written in the node.
The mechanism by which an option line is chosen is unspecified.
Only a single option line must be chosen.

#### Conditional

The _conditional_ is a command which provides addtional data about the validity of the option.
The intent of the conditional is to allow the writer to give the game more information about the option.
The conditional's syntax is identical to the if command and follows all rules there, but as it is not part of flow control must not have an accompanying endif or attached block.
The conditional must be an optional component of the option line.
As the conditional is optional any option line without a conditional must be assumed to be `true`.

The implementing program must process the results of the conditional expression and provide the resulting boolean value to the other parts of the game that makes the selection.
The implementing program must not restrict the selection of invalid options.
It is the responsibility of the other components of the game to control how invalid options are to be handled.

#### Blocks

Much like with flow control options may have blocks of statements which are triggered should that option line be chosen.
Each option line may optionally have a block of statements associated with that option line.
Similar again to the flow control, if an option line is selected its associated block of must be processed by the implementing program
If an option isn't chosen the associated block must not be processed.

Unlike the flow control however there is no clear way to tell apart different blocks and options from other parts of the yarn, instead indentation is used to determine blocks and the end of the options.
The rules for this must be followed:

The first option line in the options determines the base indentation for the options statement, this is determined by counting the number of whitespace elements before the `->` symbol.
Any statements following the option line at a greater level of indentation counts as part of the block for that option line.
Any other options lines with the same indentation is considered a new option line and closes the block for that option.

These rules are repeated for each option line until a non-option line with the same, or less indentation than the base indentation is encountered which closes the block and the option statement entirely.

Options can be nested inside options.
Not every option line needs to have blocks.
The maximum number of supported indentation of options inside a block is unspecified.

##### Tabs vs Space

The choice to require either tabs or spaces over the other is unspecified.
Tabs and spaces shouldn't be mixed.
Should there be a need to convert between them the conversion rate must be the same at all points in the project.
The rate of conversion between tabs to spaces, and spaces to tabs, is unspecified.
If there is a need to choose one, tabs should be preferred due to their improved accessibility over spaces.

#### Examples

```
-> Hi
-> Hi {$name}
```

The above is an example of an option with two choices for the player to make.
The first is a regular lines of dialogue, the second is an interpolated line of dialogue.

```
-> Hi
-> Hi Fred <<if 5 > 3>>
```

The above is an example of an option with two choices for the player to make.
Both have regular lines of dialogue.
The second has a conditional component, the validity of the second option line will be `true`.

```
-> Hi
    So, are we doing this?
    Yes, lets.
-> Hi Fred
    What's the plan?
    We're doing it.
Alright!
```

The above is an example of an option with two choices and another line of dialogue after the option.
Both are a regular lines of dialogue and both have an attached block.
If the first option was selected then the lines to be presented would be as follows:
```
So, are we doing this?
Yes, lets.
Alright!
```

```
-> Hi Fred
    What's the plan?
    We're doing it.
    -> Alright!
        Yep
    -> Ok.
-> Hi
```

The above is an example of an option with nested options in its block.
The `Alright` and `Ok` option lines are inside the `Hi Fred` option line's block.
The `Yep` line would only ever be presented if the `Hi Fred` option was selected and then the `Alright` option was selected after that.

```
-> Hi
-> Hi Fred <<if 5 > 3>>
    what's the plan?
    We're doing it.
    -> Alright!
        Yep
    -> Ok
-> Hello {$name} <<if $formality > 2 >>
-> Hi {$name}
```

The above is an example of an option with multiple option lines, conditionals, interpolated dialogue, nested options, and blocks.

## Expressions

_Expressions_ are mathematical chains of variables, values, functions, expressions, and operators that produce a single value as output.

Expressions are not a statement but are a component of various statements and must only be used as part of a statement, they cannot exist in isolation.
This means if you do want to show the result of an expression it will have to be wrapped inside an interpolated line.
For example a line that is just `$numberOfCoins + 1` is and invalid line despite containing a valid expression, but `{$numberOfCoins + 1}` is valid.

Expressions are mostly used to control the flow of the if statement, although they are also used as part of set statements, and in interpolated dialogue.

### Values

A _Value_ is a single concrete form of one of the supported types.
All expressions, subexpressions, variables and functions, must resolve down into a value before it can be used.
Examples of values include `1`, `true`, `"General Kenobi"`.

### Supported Types

Yarn supports the following types and these must be supported by an implementing program:

- number
- boolean
- string

The precision, storage, and form of the number internally by the implementing program is unspecified, however it must support decimals.
As an example of this in C# the `Decimal`, `Complex`, and `float` formats are valid (though some make more sense than others) but `int` is not.
Numbers in expressions can be written as either integers or as decimals, but decimal numbers must use the `.` symbol as the decimal seperator of the the number.

Strings must be capable of holding utf-8 values as this is what the yarn language is written in, but the internals of this is unspecified provided all valid utf-8 characters are supported.
Strings in expressions must be encapsulated between `"` and `"` symbols.

Booleans must be capable of representing the boolean logic values of `true` and `false`, however the specific implementation is undefined.
Booleans must not be exposed as `1` and `0` to expressions even if they are represented this way internally by the implementing program.
Booleans in expressions must be written as `true` for true and `false` for false.

Additional types supported are unspecified but should not be used.

### Variables

_Variables_ are a means of associating a value with a name so that it can be more easily used and changed in multiple places.
Variables can only be used inside of expressions.

#### Naming and scope

All variables are a variant on identifiers.
Variables are an identifier that start with a `$` symbol and otherwise follow all other identifier rules.
The minimum and maximum length of a variable name is unspecified but must be at least one character after the `$` symbol.

Variable names must be unique throughout their scope.
The _scope_ of the variable is what defines for how long a variable exists.
Once a variable leaves its scope it is no longer valid and implementing programs must not support accessing variables outside of their scope.
The scope of variables is undefined, however implementing programs should err on the side of variables being global in scope.

`$name` is an example of a variable name, `$𐃩` is another example of a variable name.

#### Types

Yarn is a statically typed language, in the context of Yarn this means variables have a type which represents which of the supported type's values it can hold.
Once a variable has its type determined either by declaration or inference it cannot change.
The implementing program must not allow variables to hold values of types different from its own.

Due to the nature of elements of Yarn being outside of the control of Yarn, notably functions, its possible for this requirement to be breached due to no fault of the implementing program or the Yarn script.
However In these circumstances the implementing program must generate an error.

### Operations

_Operations_ are mathematical functions that take operands and an operator and result in a new value.
Operations can have one or two operands depending on the specific operation.
_Operators_ are the symbol used to define which operation is being called.
_Operands_ are the elements that used in the operation.
Operands must be a value, a variable, an expression, or a function.
Most operations are binary operations and have two operands, these go either side of the operator, and are called the l-value and r-value for the left and right side respectively.

The following binary operations and their operator must be supported.
Some of these have multiple operators, these must work identically and exist for people who prefer to use words instead of symbols:

- addition: `+`
- subtraction: `-`
- multiplication: `*`
- division: `/`
- truncating remainder division (modulo): `%`
- equality: `==` or `is`
- inequality: `!=`
- greater-than: `>`
- less-than: `<`
- greater-than-or-equal: `>=`
- less-than-or-equal: `<=`
- boolean OR: `||` or `or`
- boolean AND: `&&` or `and`
- boolean XOR: `^` or `xor`

The amount of whitespace between operands and operators in binary operations is unspecified.

There are two unary operations that have only a single operand.
The operator always goes to the left side of the operand and the must be no whitespace between the operator and operand.
The unary operations are:

- minus: `-`
- boolean NOT: `!`

Parentheses are a special form of operation.
They are for bundling up elements of an expression into a subexpression.
Parentheses must be treated as if they are a single operand in other operations.
Parentheses start with the open bracket symbol `(` and can have any expression inside of them before being closed with the closing bracket symbol `)`.
`2 * (3 + $coins)` is an example of an expression with a parentheses operation, in this case the `3 + $coins` component must be resolved into a value before being able to be multiplied by two.

The `+` operator when operating on strings is not addition in the mathematical sense but concatenation.

#### Supported types in operations

The following table shows the compatible types for each binary operation and must be supported:

|          | + | - | * | / | % | == | != | >  | <  | >=  | <=  | \|\| | && | ^ |
|----------|---|---|---|---|---|----|----|----|----|-----|-----|------|----|---|
| numbers  | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅  | ✅ | ✅ | ✅ | ❌ |
| strings  | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | ❌  | ❌ | ❌ | ❌ | ❌ |
| booleans | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | ❌  | ❌ | ✅ | ✅ | ✅ |

The following table shows the compatible types for each unary operation and must be supported:

|          | ! | - |
|----------|---|---|
| numbers  | ❌ | ✅ |
| strings  | ❌ | ❌ |
| booleans | ✅ | ❌ |

Operations between different or incompatible types is unspecified but should not be supported due to the potential confusion.
If however they are supported existing behaviour around transitivity, commutativity, and symmetry of operations should be respected.
For example if `"hello" < 5` is `true` then `5 > "hello"` should also be `true`.
Likewise `("hello" + 2) < 5` resulted in `true` then `(2 + "hello") < 5` should also be `true`.
This taken to the extreme should mean that `1 + "hello" == "hello + 1` should evaluate to `true` which is confusing to most people, hence the recommendation against supporting operations between disparate types.

#### Operation Output

The following table shows the expected output type of each operation based on its operand type:

|         | +      | -      | *      | /      | %      | ==      | !=      | >       | <       | >=      | <=      | \|\|    | &&      | !       | unary minus |    ^    |
|---------|--------|--------|--------|--------|--------|---------|---------|---------|---------|---------|---------|---------|---------|---------|-------------|---------|
| number  | number | number | number | number | number | boolean | boolean | boolean | boolean | boolean | boolean | boolean | boolean |         | number      |         |
| string  | string |        |        |        |        | boolean | boolean |         |         |         |         |         |         |         |             |         |
| boolean |        |        |        |        |        | boolean | boolean |         |         |         |         | boolean | boolean | boolean |             | boolean |

#### Order of Operations

The order of operations is as follows:

1. parentheses (`()`)
1. boolean NOT (`!`), unary minus (`-`)
1. multiplication (`*`), division (`/`), truncating remainder division (`%`)
1. subtraction (`-`), addition (`+`)
1. equality (`==` or `is`), inequality (`!=`), less-than (`<`), greater-than (`>`), less-than-or-equal (`<=`), greater-than-or-equal (`>=`)
1. boolean OR (`||` or `or`), boolean AND (`&&` or `and`), boolean XOR (`^` or `xor`)

If there are any equal priority operations in an expression they are resolved left to right as encountered in the expression.

### Functions

_Functions_ are an alternate way of getting values into expressions.
Functions are intended to be used to allow more complex code be bundled and called in a different environment, such as in the game itself.
Functions must return a value.

#### Structure 

Functions are comprised of a name, parentheses, and parameters.

The _function name_ can be any combination of the characters `a` - `z`, `A` - `Z`, and `0` - `9`, but must not start with a number.
The minimum and maximum length of function names is unspecified.

The parentheses go after the function name and there must be no whitespace between the opening parentheses `(` and the function name.
The closing parethensis `)` finishes the function.

_Parameters_ go in between the opening `(` and closing `)` parentheses.
Parameters must be expressions, functions, values, or variables.
Functions can have multiple parameters, these must be separated by the comma `,` symbol.

Whitespace between parameters and the separator is undefined. hmm this allows for newlines is that ok?
The maximum and minimum number of allowed parameters a function can have is undefined.

Examples of functions include the following;

```yarn
getPlayerName()
DetermineCurrentRoom($playerName, $target, 2)
rad2Deg(1.5707963268)
```

#### Handling

The handling of functions by the implementing program is unspecified, however the output type of a function must not change at runtime between calls.
The implementing program should allow external parts of the game to provide the return value of the function.
If given the same input parameters multiple invocations of the same functions should return the same value each time.
