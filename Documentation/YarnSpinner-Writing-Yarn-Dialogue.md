# Writing Yarn Dialogue

### Style Guide

`Inline Code` contain short snippets of code for your project

    Code Blocks contain segments or chunks of code for your project

**Bold indicate actions (Select menu item, copying file, etc.)**

***Bold italic text indicates emphasis***

> Blockquotes contain essential information

## Tutorial

> ***Note:*** This tutorial assumes that you know nothing about writing code, in particular how to use [Unity](http://www.unity3d.com) or the Yarn Editor.
> You do not need to know how to use those tools to write Yarn Dialogue. It is required that you know how to create a text file (.txt), which is different to a .doc, .docx, .rtf etc. document, in whatever text editor you are most comfortable using. Please refer to your friendly local computer expert if you are unsure how to do this.
> As you learn more about Yarn Dialogue, you may find the Yarn Editor useful as you progress with your authoring. It is a text editor that is designed to make Yarn Dialogue easier to create. 

## Introducing Yarn Dialogue

Yarn Dialogue is designed for Authors who have little or no programming knowledge. It makes no assumptions about how your game presents dialogue to the player, or about how the player chooses their responses. These tasks are left to the game production team, using programming and design tools such as Unity3D and C#.

### Simple Example Script

We will create thei essential Yarn Dialogue for the [Simple Example Dialogue](Unity/Assets/Yarn Spinner/Examples/Demo Assets/Simple Example Script.yarn.txt). We will start off with the text as presented by the ingame characters, add in choices which are essentially what the player says to the characters, and have the characters react to the choices.

Each section of text in Yarn Dialogue is known as a ***node***.

A ***node*** consists of ***header data*** and ***body data***. The header data provides information about the body data, and the body data contains dialogue and options relating to the dialogue.

In this example, our first node commences with two characters, A and B.

We need to give this node a title, and in this example we will call it
***start***. There is no default title, it can be anything you like.
```
title: start
```

> Node titles are case insensitive. The titles ***start***, ***Start***, ***START*** and ***StArT*** are different.
> There is no default title. You will need to ensure that your game programmers know which node your game starts on, as it may not necessarily be the first node in your Yarn Dialogue file.

The title tag is part of a collection of data known as ***header data*** that can refer to position, colour and other information about the dialogue. While the title tag is required, these other header data tags are optional and generally used later in game development when the dialogue is attached to scenes and graphical characters.

To seperate the ***header data*** from the ***body data*** in the node, we then add in three `-` characters, known as a header delimiter.
````
---
```
We can now add the starting text for our characters A and B. Their dialogue is so:
```
A: Hey, I'm a character in a script!
B: And I am too! You are talking to me!

```
To indicate the end of the node, we add three `=` characters. This is called a ***node delimiter***
```
===
```

Your ***node*** should now look like this
```
title: start
---
A: Hey, I'm a character in a script!
B: And I am too! You are talking to me!
===
```

Congratulations, you've just written your very first piece of Yarn Dialogue!

### Adding options

The next step we'll take is to add in some options to present to the player.  We wish to present the player with the choice of two options. To do this, we use the `->` marker as the first characters in our line of text to tell the system the text is an option.

We add this after the A and B character dialogue, but before the ending tag.
```
-> What's going on
-> Um ok
```

Our Yarn Dialogue now reads as like this:
```
title: start
---
A: Hey, I'm a character in a script!
B: And I am too! You are talking to me!
-> What's going on
-> Um ok
===
```

