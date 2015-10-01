using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class DialogueUnityImplementation : MonoBehaviour {
	public abstract IEnumerator RunLine(string text);
	public abstract IEnumerator RunOptions(IList<string> options, Yarn.OptionChooser optionChooser);
	public abstract IEnumerator RunCommand(string text);
	public abstract IEnumerator DialogueComplete();
}

public class DialogueRunner : MonoBehaviour {

	// The JSON file to load the conversation from
	public TextAsset sourceText;

	// Our conversation engine
	private Yarn.Dialogue dialogue;

	// Our variable storage
	private Yarn.Continuity continuity;

	// The object that will handle the actual display and user input
	public DialogueUnityImplementation implementation;

	// Where to start from
	public string startNode = Yarn.Dialogue.DEFAULT_START;

	// Use this for initialization
	void Start () {

		// Ensure that we have our Implementation object
		if (implementation == null) {
			Debug.LogError("Implementation was not set!");
			return;
		}

		// Set up the variable store
		continuity = new Yarn.InMemoryContinuity();

		// Create the main Dialogue runner, providing ourselves as the
		// Implementation object
		dialogue = new Yarn.Dialogue(continuity);

		// Set up the logging system.
		dialogue.LogDebugMessage = Debug.Log;
		dialogue.LogErrorMessage = Debug.LogError;

		// Load the JSON for this conversation
		dialogue.LoadString(sourceText.text);
	}

	public void StartDialogue() {
		// Get it going
		StartCoroutine(RunDialogue());
	}

	IEnumerator RunDialogue() {

		// BOOM. DIALOGUE.
		foreach (var step in dialogue.Run(startNode)) {

			if (step is Yarn.Dialogue.LineResult) {

				// Wait for line to finish displaying
				var line = step as Yarn.Dialogue.LineResult;
				yield return StartCoroutine(this.implementation.RunLine(line.text));

			} else if (step is Yarn.Dialogue.OptionSetResult) {

				// Wait for user to finish picking an option
				var optionSet = step as Yarn.Dialogue.OptionSetResult;
				yield return StartCoroutine(
					this.implementation.RunOptions(
						optionSet.options, 
						optionSet.chooseResult
					));

			} else if (step is Yarn.Dialogue.CommandResult) {

				// Wait for command to finish running
				var command = step as Yarn.Dialogue.CommandResult;
				yield return StartCoroutine(this.implementation.RunCommand(command.command));

			}
		}
	}




}
