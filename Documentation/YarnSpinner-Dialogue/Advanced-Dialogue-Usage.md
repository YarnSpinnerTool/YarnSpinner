# Yarn Dialogue - General Usage Guide

This document talks about how to use Yarn if you're using it to write content. It doesn't talk about how to integrate Yarn Spinner into your project; for that, see ["Using Yarn Spinner in your Unity game"](../YarnSpinner-Unity).

## Lines

Yarn lets you define multiple chunks of dialogue that are linked together. Each chunk is called a *node*.

Nodes are filled with different kinds of text. The simplest is lines of dialogue, which look like this:
```
Alice: Hey, I'm a character speaking in a dialogue!
Bob: Wild!
```
Each line is delivered to your game one at a time.

## Options

To link a node to another node, you provide *options*. Options look like this:
```
[[Go to the woods|GoToWoods]]
[[Go back to the city|GoToCity]]
```
Options have two parts: the label, which is shown to the user, and the name of the node that we should link to when the user selects the option. The label and the node name are separated by a vertical bar (`|`).

You don't have to provide the label. Instead, you can just provide the node name, like so:
```
[[GoToCity]]
```
If an option has no label, Yarn will automatically jump to that node when that line is run, and it won't show options to the player.

When all of the lines in a node have been displayed, the dialogue system gives your game the list of available options. You then let the user choose an option, and then continue loading lines of dialogue, one at a time.

If there are no options when we reach the end of a node, then we've reached the end of a conversation, and Yarn Spinner will let your game know.

## Shortcut Options

Sometimes, you'll want to add little branches to your conversation, but you don't want to create separate nodes for them. Instead, you can use *shortcut options*:
```
Mae: What did you say to her?
-> Nothing.
    Mae: Oh, man. Maybe you should have.
-> That she was an idiot.
    Mae: Hah! I bet that pissed her off.
Mae: Anyway, I'd better get going.
```
When this is run, Yarn Spinner will behave just as if you'd broken all of this up into multiple nodes.

You can also attach conditions to shortcut options, which will make them only appear if a certain condition passes:
```
Bob: What would you like?
-> A burger. <<if $money >= 5>>
    Bob: Nice. Enjoy!
-> A soda. <<if $money >= 2>>
    Bob: Yum!
-> Nothing.
Bob: Thanks for coming!
```
Note that in the last example, there wasn't any attached text. If the player selected the last option ("Nothing."), then the next line to appear would be "Bob: Thanks for coming!"

You can also nest these shortcut options, if you like. Be careful, though - too much nested options can make your text difficult to read.

## Commands

In addition to showing lines of dialogue, your game will probably want to run actions - things like "move the camera to position X", or "make character start smiling". You can use commands for this.

Commands look like this:
```
<<move alice to under_bridge>>
```
Any text inside the double-chevrons will be sent directly to your game as a string. It's up to you to decide what to do with that string.

Yarn has a special command, called `stop`. If you include `<<stop>>` in your node, Yarn will stop the entire conversation when it reaches it.

## Variables and If statements

You can store numbers in variables. To do this, use the `set` command:
```
<<set $door_unlocked to 1>>
```
You can check the value of a variable using the `if` command:
```
<<if $door_unlocked is 1>>
    The door is unlocked! (This will only appear if $door_unlocked is equal to 1.)
<<endif>>
```
## Expressions

Maths is easy in Yarn.
```
<< set $number_of_stars_collected = 1 >>
<< set $number_of_stars_collected = $number_of_stars_collected + 1 >>
```
> ***Note:*** `to` and `=` are synonyms. It is up to you which you use. However it is strongly recommended to maintain consistency through your Dialogue for readability purposes. At the end of this document, you will find a [list of operator synonyms](#operator-synonyms).

[Order of operations](https://en.wikipedia.org/wiki/Order_of_operations) is as expected, but usage of brackets is encouraged for readability purposes.
```
<< set $globular_clusters_collected = 2 >>
<< set $number_of_stars_collected = $number_of_stars_collected + ( $globular_clusters_collected * 1000 ) >>
```
You can also do maths inside an `if` command. For example:
```
<<if $number_of_stars_collected > 5>>
    You have more than 5 stars!
<<endif>>
```
You can also get fancier, and do stuff like this:
```
<<if $hostages_saved == $number_of_hostages and $time_remaining > 0>>
    You win the game!
<<elseif $hostages_saved < $number_of_hostages and $time_remaining > 0>>
    You need to rescue more hostages!
<<elseif $bomb_has_exploded == 0>>
    You failed to rescue the hostages before time ran out!
<<endif>>
```
## Types

There are four different types of value in Yarn: strings, numbers, booleans, and `null`.

* Strings contain text.
* Numbers are floating-point numbers.
* Booleans are either true or false.
* `null` means no value.

Yarn will automatically convert between types for you. For example:
```
<<if "hi" == "hi">>
    The two strings are the same!
<<endif>>

<<if 1+1+"hi" == "2hi">>
    Strings get joined together with other values!
<<endif>>
```
**Warning:** Currently, variables can only store numbers. If you try to store anything else in a variable, it will get converted to a number first.

## Functions

You can call functions in your `if` commands. For example, Yarn Spinner provides a function called `visited`, which you can use to find out if a node has been entered before or not. It takes a single parameter (which is a string), and returns true or false depending on whether or not the node you specified has been entered.
```
<<if visited("GoToCity")>>
    We have gone to the city before!
<<endif>>
```
You can't define your own functions inside Yarn itself, but they can be added at run-time. See ["Extending Yarn Spinner"](../YarnSpinner-Programming/Extending.md) for more info.

## Operator synonyms
### Assignment
| word | symbol
|:---:|:---:|
| `to` | `=` |

### Comparison
| word | symbol
|:---:|:---:|
| `and` | `&` |
| `le`  | `<` |
| `gt`  | `>` |
| `or`  | `\|\|` |
| `leq` | `<=` |
| `geq` | `>=` |
| `eq`  | `==` |
| `is`  | `==` |
| `neq` | `!=` |
| `not` | `!` |
