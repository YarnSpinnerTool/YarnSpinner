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
				dialogue.LoadString (source.text);
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
					yield return StartCoroutine (this.dialogueUI.RunCommand (commandResult.command));
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

		// The conversation has end.
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
		
		public virtual void Clear ()
		{
			throw new System.NotImplementedException ();
		}
		
		public abstract void ResetToDefaults ();
		
	}

}
