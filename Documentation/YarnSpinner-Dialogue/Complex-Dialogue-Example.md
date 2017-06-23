# Yarn Dialogue - Complex Example

This document talks about how to use Yarn if you're using it to write content. It doesn't talk about how to integrate Yarn Spinner into your project; for that, see ["Using Yarn Spinner in your Unity game"](../YarnSpinner-Unity).

## Complex example
In our complex example, we will establish a multiple file dialogue. This dialogue will take place between three characters: the Player, an NPC called Sally and an NPC called Ship.

A limited amount of programming knowledge is required for narrative adventure dialogue creation, otherwise we cannot tell what is required to for the desired plot sequences to take place. In this example, we will introduce the concepts of ***commands*** and ***conditionals***. Commands instruct the game engine to execute specific code, such as making a noise in sequence with some text or generating a graphic or both (for example, displaying lightning and making the noise of thunder). We will demonstrate how interaction between players and NPCs can create what are known as [conditionals](https://en.wikipedia.org/wiki/Conditional_(computer_programming)), a form of [branches](https://en.wikipedia.org/wiki/Branch_(computer_science)). By following the [simple example](Simple-Dialogue-Example.md), you've already learned a little about [control flow](https://en.wikipedia.org/wiki/Branch_(computer_science)), in that how selecting certain text determines which nodes to ***jump*** to. Conditionals are only slightly more complex than jumps, as they are simply a method to determine which jumps may become available.

### Initial Dialogue Setup
First off, we'll create a background scene. Because our complex example is set inside a space ship, we''ll give the name of the node 'Ship'. We'll give this node some initial Dialogue.
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

## Yarn Dialogue Files
### Ship.yarn.txt
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

<<if $should_see_ship is true and $received_warning_from_sally is false>>
Player: Sally said you wanted to see me?
<<setsprite ShipFace happy>>
Ship: She totally did!!
<<setsprite ShipFace neutral>>
Ship: She wanted me to tell you...
Ship: If you ever go off-watch without resetting the console again...
<<setsprite ShipFace happy>>
Ship: She'll flay you alive!
<<set $received_warning_from_sally to true>>
Player: Uh.
<<setsprite ShipFace neutral>>

<<endif>>

===
```
### Sally.yarn.txt
```
title: Sally
---
<<if visited("Sally") is false>>
Player: Hey, Sally.
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
<<if $received_warning_from_sally and not visited("Sally.Sorry")>>
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
