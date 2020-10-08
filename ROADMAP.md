# Yarn Spinner Road Map

This is the current road map we are considering for the future of Yarn Spinner.
Nothing in here is fixed. It reflects what we believe should be the focus of development.
The idea of this document is to both give us something to help plan our development but to also give you all a space to see what we are doing, and to give us input on what you consider important.

Let us know what you think about this roadmap either in the [Slack](http://lab.to/narrativegamedev) or [here on the GitHub issue we created about the road map](https://github.com/YarnSpinnerTool/YarnSpinner/issues/183).

**This document was last edited 2020-10-08**

# Releases

Here are the main releases we're currently planning:

## V2.0 - The Next Major Version

### Goal

Add voiceovers, editor API revamp, and other breaking features.

### Motivation

Major-version changes to the project are where we're allowed to make changes and improvements that break backwards compatibility. In this release, we're taking the opportunity to improve the editor API, and adding features that are tricky to add without it.

### Features

- Revamped editor API
- Voiceover support
- Addressable Assets support
- Overhauled localization support
- Simplified API for indicating when dialogue should be made to wait for actions in the game to finish
- Easier support for custom line display tools, making it easier to create custom dialogue UI to suit your game's needs
- Built-in support for annotated text, using a BBCode-style markup
- Static typing, which prevents errors like accidentally storing strings inside variables that should have numbers
- Support for defining variables and project metadata in your project, and describing what a variable is for, so that both you and the rest of your team can keep track of it


## V2.1 - The Editor Refresh

### Goal

Our goal with version 2.1 is to update our editor offering. Currently, there are two main ways you can author content for Yarn Spinner: the Yarn Editor, and the Yarn Spinner extension for Visual Studio Code. In this release, we'll update the Yarn Spinner extension for Visual Studio Code, and make it more suitable as a full-time editing environment.

### Motivation

We've had a number of people ask us for improved support for new features in Yarn Spinner in the editor, and while the Yarn Editor and the team of volunteers (❤️❤️!!) who've maintained it over the years has done some frankly astonishing work, we feel as though the current application has gone as far as it can, and we don't feel capable of maintaining the editor as it currently is while still maintaining compatibility with Yarn Spinner.

Our goals aren't to re-implement the existing features of the Yarn Editor in a new context, but rather to piggyback off the power of Visual Studio Code, and add Yarn Spinner-specific features like the node view, syntax highlighting, and code auto-completion. Doing this means that we don't need to handle application-level features, like saving, loading, and undo.

We're also keenly aware that lots of people love using the Yarn Editor in their web browser, because it's fast to use, doesn't require a download, and has all of the features they need. To support these users, we'll create a lighter, browser-based version of the editor, which has the same ability to load, edit and save Yarn Spinner scripts.

### Features

- Visual Studio Code extension upgrades to add key features like auto-completion, graphical node editing, and more
- New, updated UI design
- Web based, download-free option for browser users

## V2.2 - Quality of Life Features

### Goal

In this release, we'll add some often-requested features to Yarn Spinner for Unity, like high-quality prefab UI for common dialogue patterns, a built-in save and load system, and the ability to test your conversations in the new editor from version 2.1.

### Motivation

There's a wide range of common ways that people interact with dialogue systems. Some of the most well-known ones the [dialogue wheel](https://www.mobygames.com/images/shots/l/405433-mass-effect-xbox-360-screenshot-dialog-choices-use-a-radial.jpg) popularised by BioWare's games, or the [in-line speech bubble](https://images.squarespace-cdn.com/content/v1/529d23d2e4b0c7dd8c183826/1510803467423-01HTPF7UDHVKYI2HY57E/ke17ZwdGBToddI8pDm48kHtvQiyZJxwcQmlVHnvY0gkUqsxRUqqbr1mOJYKfIPR7LoDQ9mXPOjoJoqy81S2I8N_N4V1vUb5AoIIIbLZhVYy7Mythp_T-mtop-vrsUOmeInPi9iDjx9w8K4ZfjXt2dgEycJE-_OaANrUfzzhfBCW5dDa2u6mKVvp1i_oCWVaSpC969RuPXvt2ZwyzUXQf7Q/parisba_2017-Oct-27.jpg?format=2500w) from Night in the Woods. UI development can be tricky work, and this is especially true for dynamic text. In this release, we'll add built-in prefabs for presenting dialogue.

In the same vein, lots of developers want a simple solution for saving and loading their game. In lots of narrative-focused games, the entire game state might be entirely Yarn Spinner variables, so it makes a lot of sense to make it fast and simple to save and restore this data. We're not going to try and implement a complete save game solution - there are lots of great packages out there that do this! - but a straightforward tool for working with your Yarn Spinner data makes a lot of sense to us.

Finally, the ability to test and validate your dialogue in the same place that you're writing it is something that can save a lot of time. To help out users of the new Yarn editing solution introduced in version 2.1, we'll add the ability to run a conversation.

### Features

- Unity prefabs for common dialogue UI patterns
- Save and load system for variables
- In-editor conversation running

## V2.3 - Unreal Support

### Goal

Yarn Spinner will support the Unreal Engine.

### Motivation

Unreal is a large portion of the games community, and we've been asked multiple times by different people if Unreal is supported. We want to be able to say yes to them.
This is not to say it will be as featureful as the Unity version, although the long term goal is for feature parity.

### Features

- Create an Unreal VM capable of reading compiled Yarn bytecode
- Create Unreal Dialouge Runners and Variable Storage
- Create examples showing how to use Yarn Spinner in Unreal.
- Add documentation on using Yarn Spinner in Unreal.

# Past Releases

## ✅ V1.2

### Goal

Bug fixes and quality-of-life improvements.

### Motivation

We've had a bunch of fantastic contributions and bug fixes come in, and this release will be a great opportunity to merge them in. Additonally, because V2.0 will be a breaking change to the language and to the API, this release is ideal for any changes that don't require a change to the API or language.

### Features

- Bug fixes
- Small quality-of-life improvements

## ✅ V1.1

### Goal

To add in the two most requested features: string interpolation, and localisation! String interpolation means being able to write lines something like `Hello, ${playerName}`, and have the line appear as "Hello, Sally"; localisation means being able to translate your game's content into multiple languages, keeping in mind the different rules that languages have regarding things like how words are pluralised, how grammatical gender works, and more.

### Motivation

By far the most requested feature, string interpolation has been put off due to concerns about implementation, in a nutshell to do "proper" interpolation is far from trivial. We're also very aware of the requirements of people who are releasing their games on storefronts that require localisation.

This release will have no other features beyond those necessary to support interpolation and localisation features, as we want to ensure this is reliable. We are also concerned as to how many bugs this may cause in existing games, so we're moving carefully here.

### Features

- String interpolation
- Localisation support
- Updated examples
- Updated documentation

## ✅ V1.0

### Goal

To make it easier for projects to spin up and remain compatible with core Yarn Spinner, and to smooth out the issues of getting started using and updating Yarn Spinner.

### Motivation

Yarn Spinner has been in a lot of flux, more or less starting as a spin-off of Night In The Woods. A lot of it was heavily tied to the needs and deadlines of that specific game.
We've been working to make it more generic for _any_ narrative game, but there is still a lot of connections to the requirements of Night In The Woods.
As such, there is a lot of wonky code and half-finished concepts which users currently need to deal with. We want that to change so you no longer have to deal with this when it comes time to update or get started with Yarn Spinner.

### Features

- [x] Formal Yarn specification created and frozen
- [x] Bytecode format freeze
- [x] Compiler will generate bytecode by default instead of parsing the dialogue each time
- [x] Default to using `.yarn` as the file extension in preference to `.yarn.text`
- [x] VM will load and execute compiled bytecode
- [x] Unity importer for `.yarn` files that invokes compiler and generates bytecode assets (using a `.yarnc` file extension).
- [x] Implementation of a barebones Unity prefab (with a Canvas) that can be added into a scene to "just work"
- [x] Rework examples to be designed to be used as the basis of your own game
- [x] Modify example dialogue runner and variable storage to be the base form, instead of "just" examples
- [x] Syntax highlighter extension for Visual Studio Code
- [x] New documentation site




# FAQ

*Hang on you just posted this, how are these frequently asked?*

Ok they aren't, but I have tried to preempt some likely questions.

*How can I contact you privately about this?*

There are a few ways you can reach the development team directly:

* **Twitter**:
* * [@YarnSpinnerTool](https://twitter.com/YarnSpinnerTool)
* * [@desplesda](https://twitter.com/desplesda)
* * [@The_McJones](https://twitter.com/The_McJones)
* **Email**:
* * yarnspinner@secretlab.com.au

*Why are there no dates attached to the releases?*

Yarn Spinner is a community project and we (as in @desplesda and @McJones) as the main devs work on it in our free time or when it aligns with work.
Any Yarn Spinner work has to fit around our own work and as such would be the first candidates to be dropped if we need more time for work or our own sanity.
We don't want to put down deadlines that, at least at this stage, would be known to be arbitrary.

*I think Feature X is the most important and you've not got it listed, what gives?*

This is what we think is important, but we want to know what **you** think is important, so either let us know in the Slack or on the [issue we opened about the road map](https://github.com/YarnSpinnerTool/YarnSpinner/issues/183).
We want to build Yarn Spinner into something helpful for you as well as us.

*I think Feature Y is the most important but you've got it way down the bottom of the road map, what gives*

We've tried to group the road map releases into features and changes in a manner that we think makes sense, but we want your feedback on this stuff, so let us know.

*You've mentioned this Slack now a few times, how do I get in?*

You can join the Narrative Game Dev slack [here](http://lab.to/narrativegamedev). We hope to see you there!

*What about bug fixes? I still have a bug you've not fixed yet!*

Throughout all of the releases the intent is to try and keep the bugs to a minimum.
Some of these changes are intentionally made to clean up some of the cruft we've accumulated that make bug fixes hard.

*Why does the road map get less detailed the longer it goes on?*

Some of these changes we can see exactly how they will work and integrate, others will need to change as we get closer.
Later releases are designed to have much larger broad strokes of features instead of specifics like in V1.0.
As a release comes out we can take a look at the road map again, work out what features and needs should go into the next release and update the road map appropriately.

# Open-ended ideas

These are ideas that we don't have a roadmap for, but we'd love to see contributed from the community. If you want to work on this, [fork the repo](https://github.com/YarnSpinnerTool/YarnSpinner/fork) and send in a pull request!

* Godot Engine Support
* Game Maker Engine Support
* Improvements and new features for YS VS Code extension