using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

// Displays dialogue lines to the player, and sends
// user choices back to the dialogue system.

// Note that this is just one way of presenting the
// dialogue to the user. The only hard requirement
// is that you provide the RunLine, RunOptions, RunCommand
// and DialogueComplete coroutines; what they do is up to you.

public class DialogueUI : Yarn.Unity.DialogueUIBehaviour
{

	// The UI element that displays lines
	public Text lineText;

	// A UI element that appears after lines have finished appearing
	public GameObject continuePrompt;

	// A delegate (ie a function-stored-in-a-variable) that
	// we call to tell the dialogue system about what option
	// the user selected
	private Yarn.OptionChooser SetSelectedOption;

	[Tooltip("How quickly to show the text, in seconds per character")]
	public float textSpeed = 0.025f;

	// The buttons that let the user choose an option
	public List<Button> optionButtons;

	// The "start talking" button. This just exists to kick stuff off in the demo.
	public Button talkButton;

	void Start ()
	{
		// Start by hiding the line and option buttons
		lineText.gameObject.SetActive (false);

		foreach (var button in optionButtons) {
			button.gameObject.SetActive (false);
		}

		// Hide the continue prompt if it exists
		if (continuePrompt != null)
			continuePrompt.SetActive (false);
	}


	// Show a line of dialogue, gradually
	public override IEnumerator RunLine (Yarn.Line line)
	{
		// Hide the Talk button until the dialogue is complete
		talkButton.gameObject.SetActive (false);

		// Show the text
		lineText.gameObject.SetActive (true);

		if (textSpeed > 0.0f) {
			// Display the line one character at a time
			var stringBuilder = new StringBuilder ();

			foreach (char c in line.text) {
				stringBuilder.Append (c);
				lineText.text = stringBuilder.ToString ();
				yield return new WaitForSeconds (textSpeed);
			}
		} else {
			// Display the line immediately if textSpeed == 0
			lineText.text = line.text;
		}

		// Show the 'press any key' prompt when done
		if (continuePrompt != null)
			continuePrompt.SetActive (true);


		// Wait for any user input
		while (Input.anyKeyDown == false) {
			yield return null;
		}

		// Hide the text and prompt
		lineText.gameObject.SetActive (false);

		if (continuePrompt != null)
			continuePrompt.SetActive (false);

	}

	// Show a list of options, and wait for the player to make a selection.
	public override IEnumerator RunOptions (Yarn.Options optionsCollection, 
	                                        Yarn.OptionChooser optionChooser)
	{
		// Hide the Talk button until the dialogue is complete
		talkButton.gameObject.SetActive (false);

		// Do a little bit of safety checking
		if (optionsCollection.options.Count > optionButtons.Count) {
			Debug.LogWarning("There are more options to present than there are" +
			                 "buttons to present them in. This will cause problems.");
		}

		// Display each option in a button, and make it visible
		int i = 0;
		foreach (var optionString in optionsCollection.options) {
			optionButtons [i].gameObject.SetActive (true);
			optionButtons [i].GetComponentInChildren<Text> ().text = optionString;
			i++;
		}

		// Record that we're using it
		SetSelectedOption = optionChooser;

		// Wait until the chooser has been used and then removed (see SetOption below)
		do { 
			yield return null;
		} while (SetSelectedOption != null);

		// Hide all the buttons
		foreach (var button in optionButtons) {
			button.gameObject.SetActive (false);
		}
	}

	// Called by buttons to make a selection.
	public void SetOption (int selectedOption)
	{

		// Call the delegate to tell the dialogue system that we've
		// selected an option.
		SetSelectedOption (selectedOption);

		// Now remove the delegate so that the loop in RunOptions will exit
		SetSelectedOption = null; 
	}

	// Run an internal command.
	public override IEnumerator RunCommand (Yarn.Command command)
	{
		// Hide the Talk button until the dialogue is complete
		talkButton.gameObject.SetActive (false);

		// "Perform" the command
		Debug.Log ("Command: " + command.text);
		yield break;
	}

	// Yay we're done. Called when the dialogue system has finished running.
	public override IEnumerator DialogueComplete ()
	{
		// Show the Talk button again
		talkButton.gameObject.SetActive (true);

		Debug.Log ("Complete!");
		yield break;
	}

}
