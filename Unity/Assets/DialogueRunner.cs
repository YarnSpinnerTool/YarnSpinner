using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DialogueRunner : MonoBehaviour, Yarn.Implementation {

	// The JSON file to load the conversation from
	public TextAsset sourceText;

	// Our conversation engine
	private Yarn.Dialogue dialogue;

	// Our variable storage
	private Yarn.Continuity continuity;

	// Where to start from
	public string startNode = "Start";

	// Use this for initialization
	void Start () {

		// Set up the variable store
		continuity = new Yarn.InMemoryContinuity();

		// Create the main Dialogue runner, providing ourselves as the
		// Implementation object
		dialogue = new Yarn.Dialogue(this);

		// Load the JSON for this conversation
		dialogue.LoadString(sourceText.text);

		// Get it going
		StartCoroutine(RunDialogue());

	}

	IEnumerator RunDialogue() {

		// BOOM. DIALOGUE.
		foreach (var step in dialogue.RunConversation(startNode)) {

			// The RunLine, RunOptions and RunCommand methods below are 
			// automatically called as necessary. If you need time to
			// finish doing something, like waiting for a line to finish 
			// animating onto the screen, or waiting for the user to select
			// an option, you can just yield here until you're ready to continue.

			yield return new WaitForSeconds(0.5f);
		}
	}

	// Display a line of text on screen
	void Yarn.Implementation.RunLine (string lineText)
	{
		Debug.Log(lineText);
	}

	// Display a list of options for the user to choose from
	void Yarn.Implementation.RunOptions (IList<string> options, Yarn.OptionChooser optionChooser)
	{
		// "options" is the list of strings to show the user.
		// "optionChooser" is a delegate that takes one parameter: 
		// the selected option number. You call it when the user has made 
		// their selection, eg:

		optionChooser(0); // in this example, just pick the first 
						  // available option immediately
	}

	void Yarn.Implementation.RunCommand (string command)
	{
		// do whatever the game needs here - this is for commands 
		// like <<sit>> <<show Lori>>
		Debug.Log("Command: " + command);
	}

	void Yarn.Implementation.DialogueComplete ()
	{
		// We reached the end of the conversation.
		// Close the dialogue UI and move on with the game.
	}

	// Logging methods for both debug-level and error-level stuff
	void Yarn.Implementation.HandleDebugMessage (string message)
	{
		Debug.Log(message);
	}

	void Yarn.Implementation.HandleErrorMessage (string message)
	{
		Debug.LogError(message);
	}

	// Used to let the Dialogue system know about where the 
	Yarn.Continuity Yarn.Implementation.continuity {
		get {
			return continuity;
		}
	}

}
