# Using Yarn Spinner in your Unity game

## Yarn Spinner Quick Start

Here's how to quickly jump in to Yarn Spinner, if you're already reasonably comfortable with Unity.

* **Import the YarnSpinner package into your project.**
* **Inside the YarnSpinner folder, open the Yarn Spinner Example scene.**
* **Start the game. Play through the dialogue.**

Once you've played with it, open the Example Script file in the Yarn editor (it's in the Demo Resources folder), and make some changes to the script. Once you've done that, take a look at how `DialogueRunner.cs`, `DialogueUI.cs` and `ExampleVariableStorage.cs` work.

## Tutorial

**Note:** This tutorial assumes that you know at least a little bit about [Unity](http://www.unity3d.com). In particular, it assumes that you know [how to get around the Unity editor](getting around in unity), [how to work with game objects](working with game objects), and [how to write scripts in C#](writing scripts). If you don't know these things, check out [Unity's documentation](unity docs)!

Yarn Spinner is designed to be easy to work with in Unity. It makes no assumptions about how your game presents dialogue to the player, or about how the player chooses their responses. 

To use Yarn Spinner, you use three classes:

* `DialogueRunner`, which is responsible for loading and running your dialogue script;
* `DialogueUI`, which is reponsible for displaying the lines and dialogue choices to the player; and
* A subclass of `VariableStorageBehaviour`, which is responsible for storing the state of the conversation.

Additionally, you store your Yarn files as `.json` assets in your Unity projects.

Let's get started with Yarn Spinner!

## Tutorial

To introduce Yarn Spinner, we'll create an empty Unity project, and then build it from the ground up to run a sample conversation.

To see the finished project, [download Yarn Spinner](https://github.com/desplesda/YarnSpinner/releases) and open the [Unity folder](https://github.com/desplesda/YarnSpinner/tree/master/Unity) in the Unity editor.

### Create the Unity project

* **Launch Unity, and create a new project.** The name of the project doesn't matter.

### Import the Yarn Spinner package.

* **Import YarnSpinner.unitypackage into your project.** If you prefer, you can also install the package from the [Asset Store](TODO).

<!--
Yarn Spinner is composed of a .DLL file, and a couple of supporting scripts for Unity.

* `YarnSpinner.dll`, which does the heavy lifting involved in parsing your Yarn files, and executing them. You won't do much with it yourself; rather, you'll take advantage of...-->

Yarn dialogue is stored in .json files, which you create using the [Yarn editor](http://github.com/infiniteammoinc/Yarn). To show the dialogue in your game, you need to add it to your project as well.

* **Create a new conversation in the [Yarn editor](http://www.github.com/infiniteammoinc/Yarn),** and save it as a JSON file. (Alternatively, if you already have a dialogue file you'd like to use, go ahead and use that instead!)

* **Copy your Yarn JSON file into your project.** You're now ready to start using Yarn Spinner.

<!-- (gif of dragging in the dialogue file) -->

### Load your conversation with `DialogueRunner`

Yarn conversations are loaded and managed by a `DialogueRunner` object. This object is responsible for loading and parsing your Yarn `.json` files. It also runs the script when it's told to - for example, when you walk up to a character in your game and talk to them.

We'll start by creating an empty object, and then we'll add the `DialogueRunner` component to it.

* **Create a new empty game object. Rename it to "Dialogue Runner".**

* With the Dialogue Runner object selected, **open the Component menu, and choose Scripts → Yarn Spinner → Dialogue Runner.**

<!-- (gif of adding component) -->

Next, you need to add the Yarn files that you want to show. The Dialogue runner can load multiple Yarn files at the same time; the only requirement is that **no nodes are allowed to have the same name**. (This is a requirement that may change in the future.)

* **Drag your Yarn JSON file into the `Source Text` array.**

<!-- (gif of adding dialogue file) -->

### Display your conversation with `DialogueUI`

Your game's dialogue needs to be shown to the user. Additionally, you need a way to let the player choose what their reaction is going to be.

Yarn Spinner makes no assumptions about how you want to handle your dialogue's UI. Want to present as simple list of options? That's fine. Want a fancy Mass Effect style radial menu? Totally cool. Want a totally bonkers gesture-based UI with a countdown timer? Oh man that would be sweet.

Yarn Spinner leaves all of the work of actually presenting the conversation up to you; all it's responsible for is delivering the lines that the player should see, and notifying Yarn Spinner about what response the user selected.

Yarn Spinner comes with a simple prefab that uses Unity's UI system. It's a good place to start. 

* **Drag in the Dialogue UI Controller prefab into your scene.**

* **Select the Dialogue Runner,** and then **drag the Dialogue UI Controller into the `Dialogue UI` slot**.

### Store your conversation state with a `VariableStorageBehaviour`

There's one last necessary component. As you play through a conversation, you'll probably want to record the user's choices somewhere. Yarn Spinner doesn't care about the details of how you save your game state; instead, it just expects you to give it an object that conforms to a C# *[interface](C# interface)*, which defines methods like "set variable" and "get value of variable".

The simplest implementation of this is one that just keeps your variables in memory, but it's pretty straightforward to adapt an existing save game system to use it.

Do one of the following two things:

* **Create a new game object, and add the `ExampleVariableStorage` script to it.**

Or:

* **Create a new game object, and add a new script to it. Make this script subclass `VariableStorageBehaviour`, and the implement the  `SetNumber`, `GetNumber`, `Clear`, and `ResetToDefaults` methods.**

* Once you've done that, **drag this new object into the Dialoge Runner's `Variable Storage` slot.**

### Run your conversation

There's only one thing left to do: Yarn Spinner just needs to know what node in the Yarn file to start from. It will default to "Start", but you can override it.

* **Change the Dialogue Runner's `Start Node` to the name of the node you'd like to start run.**

* Finally, **run the game.** The conversation will play!

		