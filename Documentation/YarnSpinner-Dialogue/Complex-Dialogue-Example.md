# Yarn Dialogue - Complex Example

### Style Guide

`Inline Code` contain short snippets of code for your project

    Code Blocks contain segments or chunks of code for your project

**Bold indicate actions (Select menu item, copying file, etc.)**

***Bold italic text indicates emphasis***

> Blockquotes contain essential information

## Tutorial

> ***Note*** This document talks about how to use Yarn if you're using it to write Yarn Dialogue. It is strongly suggested you have first read through the [Simple Example](Simple-Dialogue-Example.md) document. Neither the Simple Example or this document talk about how to integrate Yarn Spinner into your project; for that, see ["Using Yarn Spinner in your Unity game"](../YarnSpinner-Unity).

## Complex Example
In our Complex Example, we will establish a multiple file dialogue. This dialogue will take place between three characters: the Player, an NPC called Sally and an NPC called Ship.

A limited amount of programming knowledge is required for narrative adventure dialogue creation, otherwise we cannot tell what is required to for the desired plot sequences to take place. In this example, we will introduce ***[commands](https://en.wikipedia.org/wiki/Command_(computing))*** as well as  ***[conditionals](https://en.wikipedia.org/wiki/Conditional_(computer_programming))*** and ***[functions](https://en.wikipedia.org/wiki/Function_composition_(computer_science))*** to produce a more reactive narrative.

* ***Commands*** instruct the game engine to execute specific code, such as making a noise in sequence with some text or generating a graphic or both (for example, displaying lightning and making the noise of thunder).
* ***Conditionals*** are a form of [branches](https://en.wikipedia.org/wiki/Branch_(computer_science)). By following the [simple example](Simple-Dialogue-Example.md), you've already learned a little about [control flow](https://en.wikipedia.org/wiki/Branch_(computer_science)), in that how selecting certain text determines which nodes to ***jump*** to. Conditionals are only slightly more complex than jumps, as they are simply a method to determine which jumps may become available and when.
* ***Tests*** utilise a combination of variables, functions to contruct a ***conditional***.
* ***Functions***, also known in other programming languages as subroutines, procedures, or subprograms, are an external set of commands that perform a specific task. In this example, we use a function called 'Visited' to see if a node has been reached by a player in the context of a narrative story. Based upon this visit, or lack thereof, different narrative events take place.
* ***Variables*** hold pieces of information relating to the state of the game that may change depending on events happening in the game.  For example, if a player picks up sticks the number of sticks can be stored in a variable called '$sticks_collected'
> ***Note*** Variable names ***MUST*** always start with the $ character otherwise Yarn Spinner will freak the crap out.

### Initial Dialogue Setup
First off, we'll create a background scene. Because our Complex Example is set inside a space ship, we''ll give the name of the node 'Ship'. We'll give this node some initial Dialogue.
```
Ship: Hey, friend.
Player: Hi, Ship.
Player: How's space?
Ship: Oh, man.
```
### Commands
Next, we'll add a ***command*** to make the face of the ship change, the ship to say some text, then the ship return to neutral.
```
<<setsprite ShipFace happy>>
    Ship: It's HUGE!
<<setsprite ShipFace neutral>>
```
Do not be concerned that you do not have any graphics for the ShipFace.  Nor, in fact, don't be too concerned if you'd rather call the graphic FaceOfShip. In fact, the name can be anything. However to be compatible with the existing example, for comparison sake we are using ShipFace.

Our dialogue now reads
```
Ship: Hey, friend.
Player: Hi, Ship.
Player: How's space?
Ship: Oh, man.
<<setsprite ShipFace happy>>
    Ship: It's HUGE!
<<setsprite ShipFace neutral>>
```
This is some simple introductory text for our Ship character, but it wouldn't make much sense to repeat this if our player has already visited Ship. So we will need to write a little bit of code to ensure that the second time we visit Ship, it acts differently. We need to check the state of having ***visited*** the ship.

### Conditionals and Functions
Yarn Spinner has an inbuilt ***function*** called `visited`. This function checks to see if a node has previously been accessed by the player. We can combine this function with a ***conditional*** to see whether a node has been previously displayed to the player. The basic syntax of such a conditional is:
```
<<if visted("NodeName") is true>>
    NPC: You have visited me before, player
<<else>>
    NPC: You have not visited me before, player
<<endif>>
```
> ***Note:*** The above can be re-written to provide the same outcome by reversing the test from true to false and inverting the NPC text:
```
<<if visted("NodeName") is false>>
    NPC: You have not visited me before, player
<<else>>
    NPC: You have visited me before, player
<<endif>>
```
From this basic understanding of the ***'if then else'*** conditional, and usage of the `visited` function, we can then establish whether a player has visited Ship and change the dialogue Ship presents:
```
<<if visited("Ship") is false>>
    Ship: Hey, friend.
    Player: Hi, Ship.
    Player: How's space?
    Ship: Oh, man.
    <<setsprite ShipFace happy>>
        Ship: It's HUGE!
    <<setsprite ShipFace neutral>>
<<else>>
    <<setsprite ShipFace happy>>
        Ship: Hey!!
    <<setsprite ShipFace neutral>>
<<endif>>
```
>***Note:*** Remember, if we have `if visited("Ship") is true`, the order of the content of the ***'if then else'*** would need to be reversed to ensure the correct text is presented for when Player has previously visited Ship.

We will now add in some dialogue that responds to whether we've interacted with another NPC, Sally. Sally's nodes will be contained within a seperate text file for ease of editing purposes. You can contain all Yarn Dialogue nodes in the same file but you will find that it becomes increasingly difficult to maintain as your file grows longer, so we strongly encourage this separation of characters. This is the remainder of the text required for the Ship Node of Yarn Dialogue.
```
<<if $should_see_ship is true and $sally_warning is false>>
    Player: Sally said you wanted to see me?
    <<setsprite ShipFace happy>>
    Ship: She totally did!!
    <<setsprite ShipFace neutral>>
    Ship: She wanted me to tell you...
    Ship: If you ever go off-watch without resetting the console again...
    <<setsprite ShipFace happy>>
    Ship: She'll flay you alive!
    <<set $sally_warning to true>>
    Player: Uh.
    <<setsprite ShipFace neutral>>
<<endif>>
===
```
### Variables and Tests
We can see from this code that there a coupl of new things in this code snipped. They are `$should_see_ship is true` and `$sally_warning is false`.

`$should_see_ship` and `$sally_warning` are examples of what are known as a ***variable***. In this case, they are a variable of [boolean data type](https://en.wikipedia.org/wiki/Boolean_data_type) and both `$should_see_ship is true` and `$sally_warning is false` are known as [boolean expressions](https://en.wikipedia.org/wiki/Boolean_expression). In simple terms, a boolean data type can hold one of two different states, true or false. By setting the variable to either of these two states and later evaluating it, determinations can be made as to which section of Yarn Dialogue should be displayed.

We shall address the setting on the first boolean, `$shoud_see_ship`, in the nodes for the Sally character contained in the file [Sally.yarn.txt](../../Unity/Assets/YarnSpinner/Examples/DemoAssets/Space/Sally.yarn.txt). The boolean `$sally_warning` is set to `true` via the code `<<set $sally_warning to true>>` after Ship warns us that sally will flay us alive.
> ***Note:*** We have now completed the Yarn Dialogue for the Ship node. The source for this node can be found in the example file  [Ship.yarn.txt](../../Unity/Assets/YarnSpinner/Examples/DemoAssets/Space/Ship.yarn.txt). It is replicated below so as to be easily read here as a complete file.
```
title: Ship
---
<<if visited("Ship") is false>>
    Ship: Hey, friend.
    Player: Hi, Ship.
    Player: How's space?
    Ship: Oh, man.
    <<setsprite ShipFace happy>>
        Ship: It's HUGE!
    <<setsprite ShipFace neutral>>
<<else>>
    <<setsprite ShipFace happy>>
        Ship: Hey!!
    <<setsprite ShipFace neutral>>
<<endif>>

<<if $should_see_ship is true and $sally_warning is false>>
    Player: Sally said you wanted to see me?
    <<setsprite ShipFace happy>>
    Ship: She totally did!!
    <<setsprite ShipFace neutral>>
    Ship: She wanted me to tell you...
    Ship: If you ever go off-watch without resetting the console again...
    <<setsprite ShipFace happy>>
    Ship: She'll flay you alive!
    <<set $sally_warning to true>>
    Player: Uh.
    <<setsprite ShipFace neutral>>
<<endif>>
===
```
Our Yarn Dialogue for Sally will start out as simply as before, with her words changing depending on whether the player has previously `visited` her.
```
<<if visited("Sally") is false>>
    Sally: Oh! Hi.
    Sally: You snuck up on me.
    Sally: Don't do that.
<<else>>
    Player: Hey.
    Sally: Hi.
<<endif>>
```
The next step is to have Sally react to whether we've visited her Watch node
```
<<if not visited("Sally.Watch")>>
    [[Anything exciting happen on your watch?|Sally.Watch]]
<<endif>>
```
### [Sally.yarn.txt](../../Unity/Assets/YarnSpinner/Examples/DemoAssets/Space/Sally.yarn.txt)
```
title: Sally
---
<<if visited("Sally") is false>>
    Sally: Oh! Hi.
    Sally: You snuck up on me.
    Sally: Don't do that.
<<else>>
    Player: Hey.
    Sally: Hi.
<<endif>>

<<if not visited("Sally.Watch")>>
    [[Anything exciting happen on your watch?|Sally.Watch]]
<<endif>>
<<if $sally_warning and not visited("Sally.Sorry")>>
    [[Sorry about the console.|Sally.Sorry]]
<<endif>>
[[See you later.|Sally.Exit]]
===

title: Sally.Watch
---
Sally: Not really.
Sally: Same old nebula, doing the same old thing.
Sally: Oh, Ship wanted to see you. Go say hi to it.
<<set $should_see_ship to true>>
<<if visited("Ship") is true>>
    Player: Already done!
    Sally: Go say hi again.
<<endif>>
===

title: Sally.Exit
---
Sally: Bye.
===

title: Sally.Sorry
---
Sally: Yeah. Don't do it again.
===
```
