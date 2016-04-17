/*

The MIT License (MIT)

Copyright (c) 2015 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Yarn.Unity
{
	// DialogueRunners act as the interface between your game and YarnSpinner.

	// Make our menu item slightly nicer looking
	[AddComponentMenu("Scripts/Yarn Spinner/Dialogue Runner")]
	public class DialogueRunner : MonoBehaviour
	{
		// The JSON files to load the conversation from
		public TextAsset[] sourceText;

		// Our variable storage
		public Yarn.Unity.VariableStorageBehaviour variableStorage;

		// The object that will handle the actual display and user input
		public Yarn.Unity.DialogueUIBehaviour dialogueUI;

		// Which node to start from
		public string startNode = Yarn.Dialogue.DEFAULT_START;

		// Whether we should start dialogue when the scene starts
		public bool startAutomatically = true;

		public bool isDialogueRunning { get; private set; }

		public bool automaticCommands = true;

		// Our conversation engine
		// Automatically created on first access
		private Dialogue _dialogue;
		public Dialogue dialogue {
			get {
				if (_dialogue == null) {
					// Create the main Dialogue runner, and pass our variableStorage to it
					_dialogue = new Yarn.Dialogue (variableStorage);

					// Set up the logging system.
					_dialogue.LogDebugMessage = Debug.Log;
					_dialogue.LogErrorMessage = Debug.LogError;
				}
				return _dialogue;
			}
		}

		void Start ()
		{
			// Ensure that we have our Implementation object
			if (dialogueUI == null) {
				Debug.LogError ("Implementation was not set! Can't run the dialogue!");
				return;
			}

			// And that we have our variable storage object
			if (variableStorage == null) {
				Debug.LogError ("Variable storage was not set! Can't run the dialogue!");
				return;
			}

			// Ensure that the variable storage has the right stuff in it
			variableStorage.ResetToDefaults ();

			// Load all JSON
			foreach (var source in sourceText) {
				dialogue.LoadString (source.text, source.name);
			}

			if (startAutomatically) {
				StartDialogue();
			}
		}

		public void AddScript(string text) {
			dialogue.LoadString(text);
		}

		public void AddScript(TextAsset asset) {
			dialogue.LoadString(asset.text);
		}

		// Nuke the variable store and start again
		public void ResetDialogue ()
		{
			variableStorage.ResetToDefaults ();
			StartDialogue ();
		}

		public void StartDialogue () {
			StartDialogue(startNode);
		}

		public void StartDialogue (string startNode)
		{

			// Stop any processes that might be running already
			StopAllCoroutines ();
			dialogueUI.StopAllCoroutines ();

			// Get it going
			StartCoroutine (RunDialogue (startNode));
		}

		IEnumerator RunDialogue (string startNode = "Start")
		{
			// Mark that we're in conversation.
			isDialogueRunning = true;

			// Signal that we're starting up.
			yield return StartCoroutine(this.dialogueUI.DialogueStarted());

			// Get lines, options and commands from the Dialogue object,
			// one at a time.
			foreach (Yarn.Dialogue.RunnerResult step in dialogue.Run(startNode)) {

				if (step is Yarn.Dialogue.LineResult) {

					// Wait for line to finish displaying
					var lineResult = step as Yarn.Dialogue.LineResult;
					yield return StartCoroutine (this.dialogueUI.RunLine (lineResult.line));

				} else if (step is Yarn.Dialogue.OptionSetResult) {

					// Wait for user to finish picking an option
					var optionSetResult = step as Yarn.Dialogue.OptionSetResult;
					yield return StartCoroutine (
						this.dialogueUI.RunOptions (
						optionSetResult.options,
						optionSetResult.setSelectedOptionDelegate
					));

				} else if (step is Yarn.Dialogue.CommandResult) {

					// Wait for command to finish running

					var commandResult = step as Yarn.Dialogue.CommandResult;

					if (DispatchCommand(commandResult.command.text) == true) {
						// command was dispatched
					} else {
						yield return StartCoroutine (this.dialogueUI.RunCommand (commandResult.command));
					}


				} else if(step is Yarn.Dialogue.NodeCompleteResult) {

					// Wait for post-node action
					var nodeResult = step as Yarn.Dialogue.NodeCompleteResult;
					yield return StartCoroutine (this.dialogueUI.NodeComplete (nodeResult.nextNode));
				}
			}

			// No more results! The dialogue is done.
			yield return StartCoroutine (this.dialogueUI.DialogueComplete ());

			// Clear the 'is running' flag. We do this after DialogueComplete returns,
			// to allow time for any animations that might run while transitioning
			// out of a conversation (ie letterboxing going away, etc)
			isDialogueRunning = false;
		}

		public void Clear() {

			if (isDialogueRunning) {
				throw new System.InvalidOperationException("You cannot clear the dialogue system while a dialogue is running.");
			}

			dialogue.UnloadAll();
		}

		public void Stop() {
			dialogue.Stop();
		}

		public bool NodeExists(string nodeName) {
			return dialogue.NodeExists(nodeName);
		}

		public string currentNodeName {
			get {
				return dialogue.currentNode;
			}
		}

		public bool DispatchCommand(string command) {

			// commands that can be automatically dispatched look like this:
			// COMMANDNAME OBJECTNAME <param> <param> <param> ...

			// We can dispatch this command if:
			// 1. it has at least 2 words
			// 2. the second word is the name of an object
			// 3. that object has components that have methods 
			//    with the YarnCommand attribute that have the
			//    correct commandString set

			var words = command.Split(' ');

			// need 2 parameters in order to have both a command name
			// and the name of an object to find
			if (words.Length < 2)
				return false;

			var commandName = words[0];

			var objectName = words[1];

			var sceneObject = GameObject.Find(objectName);

			// If we can't find an object, we can't dispatch a command
			if (sceneObject == null)
				return false;

			int numberOfMethodsFound = 0;

			List<string> parameters;

			if (words.Length > 2) {
				parameters = new List<string>(words);
				parameters.RemoveRange(0, 2);
			} else {
				parameters = new List<string>();
			}

			// Find every MonoBehaviour (or subclass) on the object
			foreach (var component in sceneObject.GetComponents<MonoBehaviour>()) {
				var type = component.GetType();

				// Find every method in this component
				foreach (var method in type.GetMethods()) {

					// Find the YarnCommand attributes on this method
					var attributes = (YarnCommandAttribute[]) method.GetCustomAttributes(typeof(YarnCommandAttribute), true);

					// Find the YarnCommand whose commandString is equal to the command name
					foreach (var attribute in attributes) {
						if (attribute.commandString == commandName) {

							// Verify that this method has the right number of parameters
							var methodParameters = method.GetParameters();

							if (methodParameters.Length != parameters.Count) {
								Debug.LogErrorFormat(sceneObject, "Method \"{0}\" wants to respond to Yarn command \"{1}\", but it has a different number of parameters ({2}) to those provided ({3})!", method.Name, commandName, methodParameters.Length, parameters.Count);
								return false;
							}

							// Verify that this method has only string parameters (or no parameters)
							foreach (var paramInfo in methodParameters) {
								if (paramInfo.ParameterType.IsAssignableFrom(typeof(string)) == false) {
									Debug.LogErrorFormat(sceneObject, "Method \"{0}\" wants to respond to Yarn command \"{1}\", but not all of its parameters are strings!", method.Name, commandName);
									return false;
								}
							}

							// Cool, we can send the command!
							method.Invoke(component, parameters.ToArray());
							numberOfMethodsFound ++;

						}
					}
				} 
			}

			// Warn if we found multiple things that could respond
			// to this command.
			if (numberOfMethodsFound > 1) {
				Debug.LogWarningFormat(sceneObject, "The command \"{0}\" found {1} targets. " +
					"You should only have one - check your scripts.");
			}

			return numberOfMethodsFound > 0;
		}

	}

	// Apply this attribute to methods in your scripts to expose
	// them to Yarn.

	// For example:
	// [YarnCommand("dosomething")]
	// void Foo() {
	//    do something!
	// }
	public class YarnCommandAttribute : System.Attribute
	{
		public string commandString { get; private set; }

		public YarnCommandAttribute(string commandString) {
			this.commandString = commandString;
		}
	}

	// Scripts that can act as the UI for the conversation should subclass this
	public abstract class DialogueUIBehaviour : MonoBehaviour
	{
		// A conversation has started.
		public virtual IEnumerator DialogueStarted() {
			// Default implementation does nothing.
			yield break;
		}

		// Display a line.
		public abstract IEnumerator RunLine (Yarn.Line line);

		// Display the options, and call the optionChooser when done.
		public abstract IEnumerator RunOptions (Yarn.Options optionsCollection,
												Yarn.OptionChooser optionChooser);

		// Perform some game-specific command.
		public abstract IEnumerator RunCommand (Yarn.Command command);

		// The node has ended.
		public virtual IEnumerator NodeComplete(string nextNode) {
			// Default implementation does nothing.
			yield break;
		}

		// The conversation has ended.
		public virtual IEnumerator DialogueComplete () {
			// Default implementation does nothing.
			yield break;
		}
	}

	// Scripts that can act as a variable storage should subclass this
	public abstract class VariableStorageBehaviour : MonoBehaviour, Yarn.VariableStorage
	{

        public virtual void SetNumber (string variableName, float number)
		{
			throw new System.NotImplementedException ();
		}

        public virtual float GetNumber (string variableName)
		{
			throw new System.NotImplementedException ();
		}

		public virtual Value GetValue(string variableName) {
			return new Yarn.Value(this.GetNumber(variableName));
		}

		public virtual void SetValue(string variableName, Value value) {
			this.SetNumber(variableName, value.AsNumber);
		}

		public virtual void Clear ()
		{
			throw new System.NotImplementedException ();
		}

		public abstract void ResetToDefaults ();

	}

}
