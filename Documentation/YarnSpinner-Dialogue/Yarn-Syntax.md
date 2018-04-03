# Yarn Quick Reference

This document is intended to act as a comprehensive and concise reference for Yarn syntax and structure, for use by programmers and content creators. It assumes a working knowledge of modern programming

### Nodes

Nodes act as containers for Yarn script, and must have unique titles. The script in the body of a node is processed line by line.

A node's header contains its metadata - by default, Yarn only uses the `title` field, but can be extended to use arbitrary fields.

```
title: ExampleNodeName
tags: foo, bar
---

Yarn content goes here.
This is the second line.

===
```

A script file can contain multiple nodes. In this case, nodes are delineated using three equals (`=`) characters.

Additionally, Yarn can check if a node has been visited by calling `visited("NodeName")` in an `if` statement (i.e. `<<if visited("NodeName") == true>>`).

### Links Between Nodes

Nodes link to other nodes through *options*. An option is composed of a label (optional) and a node name separated by a vertical bar (`|`), like so:

```
[[A Link To A Node|Node1]]
```

If a node link with no label is provided (`[[Node1]]`), Yarn will automatically navigate to the linked node.

##### Menu Syntax

Shortcut options allow for small branches in Yarn scripts without requiring extra nodes. Shortcut option sets allow for an arbitrary number of sub-branches, but it's recommended that users stick to as few as possible for the sake of script readability.

```
Mae: What did you say to her?
-> Nothing.
    Mae: Oh, man. Maybe you should have.
-> That she was a jerk.
    Mae: Hah! I bet that pissed her off.
    Mae: How'd she react?
    -> She didn't.
    	Mae: Booooo. That's boring.
    -> Furiously.
    	Mae: That's what I like to hear!
Mae: Anyway, I'd better get going.
```

Additionally, shortcut options can utilize conditional logic, commands and functions (detailed below), and standard node links. If a condition is attached to a shortcut option, the option will only appear to the reader if the condition passes: <!-- TODO: CHECK UP ON SHORTCUT LOGIC -->

```
Bob: What would you like?
-> A burger. <<if $money > 5>> 
	Bob: Nice. Enjoy!
	[[AteABurger]]
-> A soda. <<if $money > 2>>
	Bob: Yum!
	[[DrankASoda]]
-> Nothing.
	Bob: Okay.
Bob: Thanks for coming!
```

##### Option Syntax
<!-- *Note: option syntax is in the process of being deprecated.* -->
Multiple labeled node links on consecutive lines will be parsed as a menu. Example:

```
[[Option 1|Node1]]
[[Option the Second|Node2]]
[[Third Option|Node3]]
```

### Variables & Conditionals

##### Declaring and Setting Variables

```
<<set $ExampleVariable to 1>>
```

This statement serves to set a variable's value. No declarative statement is required; setting a variable's value brings it into existence.

Variable names must start with a `$` character.

##### Variable Types

There are four different types of variable in Yarn: strings, floating-point numbers, booleans, and `null`.

Yarn will automatically convert between types. For example:

```
<<if "hi" == "hi">>
    The two strings are the same!
<<endif>>

<<if 1+1+"hi" == "2hi">>
    Strings get joined together with other values!
<<endif>>
```

**Note:** Currently, variables can only store numbers. If you try to store anything else in a variable, it will get converted to a number first.

##### If/Else Statements

Yarn supports standard if/else/elseif statements.

```
<<if "hi" == "hi">>
    The two strings are the same!
<<endif>>
```

```
<<if $variable == 1>>
    Success!
<<elseif $variable == "hello">>
	Success...?
<<else>>
    No success. :(
<<endif>>
```

##### Operator Synonyms

###### Assignment

| word | symbol
|:---:|:---:|
| `to` | `=` |

###### Comparison

| word | symbol
|:---:|:---:|
| `and` | `&` |
| `le`  | `<` |
| `gt`  | `>` |
| `or`  | `||` | <!-- TODO: CHECK THIS ISN'T BROKEN ON GITHUB -->
| `leq` | `<=` |
| `geq` | `>=` |
| `eq`  | `==` |
| `is`  | `==` |
| `neq` | `!=` |
| `not` | `!` |

### Commands and Functions

By default, Yarn Spinner includes a `visited()` function, used to check whether a node has been entered.
```
<<if visited("GoToCity")>>
    We have gone to the city before!
<<endif>>
```
Additional functions and commands can be added at run-time. See ["Extending Yarn Spinner"](../YarnSpinner-Programming/Extending.md) for more info.