# Yarn Quick Reference

This document is intended to act as a comprehensive and concise reference for Yarn syntax and structure, for use by programmers and content creators. It assumes a working knowledge of modern programming

### Nodes

Nodes act as containers for Yarn script, and must have unique titles. The script in the body of the node is processed line by line.

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

### Option Syntax

Nodes link to other nodes through *options*. An option is composed of a label (optional) and a node name separated by a vertical bar (`|`), like so:

```
[[Label|Node1]]
[[Label2|Node2]]
```

In this example, Yarn will give readers an option between `Label` and `Label2`, which will take them to either the `Node1` node or the `Node2` node.

If no label is provided (`[[Node1]]`), Yarn will automatically navigate to the linked node.

##### Shortcut Syntax

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

Additionally, shortcut options can utilize conditional logic, commands and functions (detailed below), and standard node links. If a condition is attached to a shortcut option, the option will only appear to the reader if the condition passes:

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
	<<set bob_face to sad>>
Bob: Thanks for coming!
```

### Variables

##### Declaring and Setting Variables

```
<<set $ExampleVariable to 1>>
```

This statement serves to set a variable's value. No declarative statement is required; setting a variable's value brings it into existence.

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

### Conditionals/Expressions

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




