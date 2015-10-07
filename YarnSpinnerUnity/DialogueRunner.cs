using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Yarn.Unity
{
	// DialogueRunners act as the interface between your game and YarnSpinner.
	public class DialogueRunner : MonoBehaviour
	{
		// The JSON file to load the conversation from
		public TextAsset sourceText;
		
		// Our conversation engine
		private Yarn.Dialogue dialogue;
		
		// Our variable storage
		public Yarn.Unity.VariableStorageBehaviour variableStorage;
		
		// The object that will handle the actual display and user input
		public Yarn.Unity.DialogueUIBehaviour dialogueUI;
		
		// Which node to start from
		public string startNode = Yarn.Dialogue.DEFAULT_START;
		
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
			
			// Create the main Dialogue runner, and pass our variableStorage to it
			dialogue = new Yarn.Dialogue (variableStorage);
			
			// Set up the logging system.
			dialogue.LogDebugMessage = Debug.Log;
			dialogue.LogErrorMessage = Debug.LogError;
			
			// Load the JSON for this conversation
			dialogue.LoadString (sourceText.text);
		}

		// Nuke the variable store and start again
		public void ResetDialogue ()
		{
			variableStorage.ResetToDefaults ();
			StartDialogue ();
		}
		
		public void StartDialogue ()
		{
			
			// Stop any processes that might be running already
			StopAllCoroutines ();
			dialogueUI.StopAllCoroutines ();
			
			// Get it going
			StartCoroutine (RunDialogue (startNode));
		}
		
		IEnumerator RunDialogue (string startNode)
		{
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
