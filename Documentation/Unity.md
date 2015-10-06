# Using Yarn Spinner in your Unity game

**Note:** This page assumes that you know at least a little bit about [Unity](http://www.unity3d.com). In particular, it assumes that you know [how to get around the Unity editor], [how to work with game objects], and [how to write scripts in C#]. If you don't know these things, check out [Unity's documentation]!

Yarn Spinner is designed to be easy to work with in Unity. It makes no assumptions about how your game presents dialogue to the player, or about how the player chooses their responses. 

To use Yarn Spinner, you use three classes:

* `DialogueRunner`, which is responsible for loading and running your dialogue script;
* `DialogueUI`, which is reponsible for displaying the lines and dialogue choices to the player; and
* A subclass of `VariableStorageBehaviour`, which is responsible for storing the state of the conversation.

Additionally, you store your Yarn files as `.json` assets in your Unity projects.

Let's get started with Yarn Spinner!

## Yarn Spinner Quick Start

To introduce Yarn Spinner, we'll create an empty Unity project, and then build it from the ground up to run a sample conversation.

To see the finished project, [download Yarn Spinner](https://github.com/desplesda/YarnSpinner/releases) and open the [Unity folder](https://github.com/desplesda/YarnSpinner/tree/master/Unity) in the Unity editor.

### Create the Unity project

* **Launch Unity, and create a new project.** The name of the project doesn't matter.

### Add your Yarn file to your project

Before you can show the dialogue in your game, you need to add it to your project. 

* **Create a new conversation in the [Yarn editor](http://www.github.com/infiniteammoinc/Yarn),** and save it as a JSON file. 

* **Copy this file into your project.** You're now ready to start using Yarn Spinner.

* **Download the Dialogue**

### Load your conversation with `DialogueRunner`

Yarn conversations are loaded and managed by a `DialogueRunner` object. This object is responsible for loading and parsing your Yarn `.json` files. It also runs the script when it's told to - for example, when you walk up to a character in your game and talk to them.



### Display your conversation with `DialogueUI`

### Store your conversation state with a `VariableStorageBehaviour`
