using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

public class DialogueImplementation : DialogueUnityImplementation {

	public Text lineText;
	public GameObject lineContinuePrompt;

	private Yarn.OptionChooser currentOptions;

	[Tooltip("How quickly to show the text, in seconds per character")]
	public float textSpeed = 0.1f;

	public List<Button> optionButtons;

	void Start() {
		// Start by hiding the line and options
		lineText.gameObject.SetActive(false);
		lineContinuePrompt.SetActive(false);

		foreach (var button in optionButtons) {
			button.gameObject.SetActive(false);
		}
	}

	// Show a line of dialogue (gradually)
	public override IEnumerator RunLine (string text)
	{
		// Show the text
		lineText.gameObject.SetActive(true);

		var stringBuilder = new StringBuilder();
		// Display the line one character at a time
		foreach (char c in text) {
			stringBuilder.Append(c);
			lineText.text = stringBuilder.ToString();
			yield return new WaitForSeconds(textSpeed);
		}

		// Show the 'press any key' prompt when done
		lineContinuePrompt.SetActive(true);

		// Wait for user input
		while (Input.anyKeyDown == false) {
			yield return null;
		}

		// Hide the text and prompt

		lineContinuePrompt.SetActive(false);
		lineText.gameObject.SetActive(false);

	}

	// Show a list of options, and wait for the player to make a selection.
	public override IEnumerator RunOptions (IList<string> options, Yarn.OptionChooser optionChooser)
	{
		// Display each option in a button, and make it visible
		int i = 0;
		foreach (var optionString in options) {
			optionButtons[i].gameObject.SetActive(true);
			optionButtons[i].GetComponentInChildren<Text>().text = optionString;
			i++;

		}

		// Record that we're using it
		currentOptions = optionChooser;

		// Wait until the chooser has been used and then removed (see SetOption below)
		do { 
			yield return null;
		} while (currentOptions != null);

		// Hide all the buttons
		foreach (var button in optionButtons) {
			button.gameObject.SetActive(false);
		}
	}

	// Called by buttons to make a selection.
	public void SetOption(int selectedOption) {
		currentOptions(selectedOption);
		currentOptions = null; // clear it to make the coroutine continue
	}

	// Run an internal command.
	public override IEnumerator RunCommand (string text)
	{
		Debug.Log("Command: " + text);
		yield return null;
	}

	// Yay we're done
	public override IEnumerator DialogueComplete ()
	{
		Debug.Log("Complete!");
		yield return null;
	}




}
