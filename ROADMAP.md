# Yarn Spinner Road Map

This is the current road map we are considering for the future of Yarn Spinner.
Nothing in here is fixed and reflects what we believe should be the focus of development.
The idea of this document is to both give us something to help plan our development but to also give you all a space to see what we are doing, and to give us input on what you consider important.

Let us know what you think about this road map either in the [Slack](http://lab.to/narrativegamedev) or [here on the GitHub issue we created about the road map](https://github.com/YarnSpinnerTool/YarnSpinner/issues/183).

**This documents was last edited 17/12/2019**

# Releases

Here are the main releases we're currently planning.

## V1.0

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
- [ ] Syntax highlighter extension for Visual Studio Code
- [ ] New documentation site

## V1.1

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

## V1.2

### Goal

To add in initial support for Unreal.

### Motivation

Unreal is a large portion of the games community, and we've been asked multiple times by different people if Unreal is supported. We want to be able to say yes to them.
This is not to say it will be as featureful as the Unity version for V1.2, although the long term goal is for feature parity.

### Features

- Create an Unreal VM capable of reading compiled Yarn bytecode
- Create Unreal Dialouge Runners and Variable Storage
- Create examples showing how to use Yarn Spinner in Unreal.
- Add documentation on using Yarn Spinner in Unreal.

## V1.3

### Goal

To create resources for developers to get more out of using Yarn Spinner.

### Motivation

At this point, Yarn Spinner should be at a state where it is stable to use and easy to update.
The intent is to then make it more convenient for developers to integrate, so more examples and ready to go elements.

### Features

- Modular prefab UI elements designed to work out of the both with Yarn Spinner
- More examples and example projects demonstrating using Yarn in different forms
- Add in parsing `.yarn` files into Unreal

## V1.4

### Goal

Make Yarn more accessible from your existing development tools.

### Motivation

While we expect the [Yarn editor](https://github.com/YarnSpinnerTool/YarnEditor) will remain the main way you will create and modify Yarn files, there are always more workflows that need support.
This release will be designed to try and help make that easier.

### Features

- Merge all command line tool functionality into the Unity and Unreal core
- Create a [language server protocol](https://microsoft.github.io/language-server-protocol/) server for Yarn
- Add diagnostics and debugging features to a VSCode extension
- Create a WebAssembly compiler for Yarn, allowing browsers to run the exact same code to compile Yarn as is used in Unity and Unreal

## V1.5

### Goal

To put Yarn Spinner in a place where it will be capable of being used more easily and in multiple places.

### Motivation

We want Yarn Spinner to be easy to implement, use, and port to as many places as possible.
We also want to start adding in support for any additional features that are community requested.

### Features

- Porting guide for any platform, engine, and language
- Attributed string support (such as `[b]this is bold text[/b]`)

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
