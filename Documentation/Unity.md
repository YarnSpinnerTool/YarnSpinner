# Using Yarn Spinner in your Unity game

Style Guide
* `Grey background`      : Style 1
* **Bold**               : Style 2
* ***Italics***          : Style 3
*      Plain Indentation : Style 4
## Yarn Spinner Quick Start

Here's how to quickly jump in to Yarn Spinner if you're already familiar with Unity.

* **Download and import [the YarnSpinner package](https://github.com/thesecretlab/YarnSpinner/releases) into your project.**
* **Inside the YarnSpinner folder, open the `Examples/Yarn Spinner Basic Example` scene.**
* **Start the game. Play through the dialogue.**

Once you've played with it, open the Example Script file in the Yarn Editor (it's in the `Examples/Demo Assets` folder), and make some changes to the script. Once you've done that, take a look at how `Code/DialogueRunner.cs`, `Examples/Demo Scripts/ExampleDialogueUI.cs` and `Examples/Demo Scripts/ExampleVariableStorage.cs` work. You can also [add your own functions to Yarn](Extending.md).

## Tutorial

> ***Note:*** This tutorial assumes that you know a little bit about [Unity](http://www.unity3d.com). In particular, it is helpful that you know how to get around the Unity editor, how to work with game objects, and how to write scripts in C#. If you don't know these things, please refer to [Unity's documentation](http://unity3d.com/learn).

Yarn Spinner is designed to be easy to work with in Unity. It makes no assumptions about how your game presents dialogue to the player, or about how the player chooses their responses. 

To introduce Yarn Spinner, we'll create an empty Unity project, and then build it from the ground up to run a sample conversation. If you'd first like to  see the finished project, [download Yarn Spinner](https://github.com/thesecretlab/YarnSpinner/releases) and open the [Unity folder](https://github.com/thesecretlab/YarnSpinner/tree/master/Unity) in the Unity editor. To build a standalone version of this loaded project, skip to the end of this documentation.

To use Yarn Spinner, you use three classes that will exist in the `Yarn.Unity` namespace.

* `DialogueRunner`, which is responsible for loading and running your dialogue script;
* A subclass of `DialogueUIBehaviour`, which is reponsible for displaying the lines and dialogue choices to the player; and
* A subclass of `VariableStorageBehaviour`, which is responsible for storing the state of the conversation.

To create your subclasses of `DialogueUIBehaviour` and `VariableStorageBehaviour`, you'll need to add the following code to the top of your C# code:

    using Yarn.Unity;

Yarn dialogue is created using the [Yarn Editor](http://github.com/infiniteammoinc/Yarn), and the resulting dialogue is stored as `.json` assets in the Unity project. If you are using Linux and wish to use the Yarn Editor, you will first need to [install](https://nwjs.io/downloads/) or [build](https://github.com/nwjs/nw.js/blob/nw22/docs/For%20Developers/Building%20NW.js.md) [NW.js](https://nwjs.io/) then attempt to build the Yarn Editor. **NOTE AT THIS STAGE, BUILDING NW.JS HAS NOT BEEN ATTEMPTED BY US AND MAY SET YOUR COMPUTER ON FIRE**.

The Yarn dialogue files can be stored anywhere inside the project hierarchy - you simply provide add them to the `DialogueRunner`'s inspector. You can also call `AddScript` on the `DialogueRunner` at runtime; this is useful for cases such as spawning a character who comes with additional dialogue - all that needs to happen is the character then pass their Yarn script to the `DialogueRunner`.

### Create the Unity project

* **Launch Unity**, and **create a new project**. The name of the project doesn't matter.

### Import the Yarn Spinner package.

* **Import YarnSpinner.unitypackage** into your project. <!-- If you prefer, you can also install the package from the [Asset Store](TODO). -->

    Yarn Spinner is composed of a .DLL file, and a couple of supporting scripts for Unity.

* `YarnSpinner.dll`, which does the heavy lifting involved in parsing your Yarn files, and executing them. You won't do much with it yourself; rather, you'll take advantage of.

    To show Yarn dialogue in your game, you will need to add it to your project as well.
    
* **Create a new conversation** in the Yarn Editor, and save it as a JSON file. (Alternatively, if you already have a dialogue file you'd like to use, go ahead and use that instead!)

* **Copy your Yarn JSON file** into your project.

You're now ready to start using Yarn Spinner!

<!-- (gif of dragging in the dialogue file) -->

### Load your conversation with `DialogueRunner`

Yarn conversations are loaded and managed by a `DialogueRunner` object. This object is responsible for loading and parsing your Yarn `.json` files. It also runs the script when it's told to - for example, when you walk up to a character in your game and talk to them.

We'll start by creating an empty object, and then we'll add the `DialogueRunner` component to it.

* **Create a new empty game object**.

* **Rename it to "Dialogue Runner"**.

* With the Dialogue Runner object selected, **open the Component menu**, and choose **Scripts → Yarn Spinner → Dialogue Runner**.

    Next you need to add the Yarn files that you want to show. The Dialogue runner can load multiple Yarn files at the same time. The only requirement is that **no nodes are allowed to have the same name**. (This is a requirement that may change in the future.)


<!-- (gif of adding component) -->


* **Drag your Yarn JSON file into the `Source Text` array.**

<!-- (gif of adding dialogue file) -->

### Display your conversation with `DialogueUI`

Your game's dialogue needs to be shown to the user. Additionally, you need a way to let the player choose what their reaction is going to be.

Yarn Spinner makes no assumptions about how you want to handle your dialogue's UI. Want to present as simple list of options? That's fine. Want a fancy Mass Effect style radial menu? Totally cool. Want a totally bonkers gesture-based UI with a countdown timer? Oh man that would be sweet.

Yarn Spinner leaves all of the work of actually presenting the conversation up to you; all it's responsible for is delivering the lines that the player should see, and notifying Yarn Spinner about what response the user selected.

Yarn Spinner comes with an example script that uses Unity's UI system. It's a good place to start. 

<!-- TODO: This needs completion.
* **Select the Dialogue Runner,** , and drag the `ExampleDialogueUI` script onto it. 

The `ExampleDialogueUI` script uses a `Text` object to display the current line of dialogue, and a number of `Button` objects to display the possible choices a player can select. When Yarn Spinner has a line of dialogue, it displays it in the text field; when Yarn Spinner has a collection of choices, each button's text is set to the corresponding choice.

 -->

### Store your conversation state with a `VariableStorageBehaviour`

There's one last necessary component. As you play through a conversation, you'll probably want to record the user's choices somewhere. Yarn Spinner doesn't care about the details of how you save your game state; instead, it just expects you to give it an object that conforms to a C# *[interface](C# interface)*, which defines methods like "set variable" and "get value of variable".

The simplest implementation of this is one that just keeps your variables in memory, but it's pretty straightforward to adapt an existing save game system to use it.


* **Create a new game object**, and add the `ExampleVariableStorage` script to it.

Or:

* **Create a new game object**, and add a new script to it. Make this script subclass `VariableStorageBehaviour`, and the implement the  `SetNumber`, `GetNumber`, `Clear`, and `ResetToDefaults` methods.

* Once you've done that, **drag this new object into the Dialoge Runner's `Variable Storage` slot.**

### Run your conversation

There's only one thing left to do: Yarn Spinner just needs to know what node in the Yarn file to start from. It will default to "Start", but you can override it.

* **Change the Dialogue Runner's `Start Node`** to the **name of the node you'd like to start run.**

* Finally, **run the game.** The conversation will play!


### Respond to commands with `YarnCommand`

In Yarn, you can create *commands* that tell your game to do something. For example, if you want a character to move to a certain point on the screen, you might have a command that looks like this:

	<<move Sally exit>>

For this to work, the game object named "Sally" needs to have a script component attached to it, and one of those scripts needs to have a method that looks like this:

	[YarnCommand("move")]
	public void MoveCharacter(string destinationName) {
		// move to the destination
	}

When Yarn encounters a command that contains two or more words, it looks for a game object with the same name as the second word ("Sally", in the above example), and then searches that object's scripts for any method that has a `YarnCommand` with the same name as the first word (in this case, "move").

Any further words in the command are passed as string parameters to the method ("exit", in this case, which is used as the `destinationName` parameter)

Note that **all** parameters must be strings. `DialogueRunner` will throw an error if it finds a method that has parameters of any other type. It's up to your method to convert the strings into other types, like numbers.

### Finishing up

Save the project

Build a stand alone

