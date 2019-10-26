/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

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
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

namespace Yarn.Unity.Example {
    /// Displays dialogue lines to the player, and sends
    /// user choices back to the dialogue system.

    /** Note that this is just one way of presenting the
     * dialogue to the user. The only hard requirement
     * is that you provide the RunLine, RunOptions, RunCommand
     * and DialogueComplete coroutines; what they do is up to you.
     */
    public class ExampleDialogueUI : Yarn.Unity.DialogueUIBehaviour
    {

        /// The object that contains the dialogue and the options.
        /** This object will be enabled when conversation starts, and 
         * disabled when it ends.
         */
        public GameObject dialogueContainer;

        /// The UI element that displays lines
        public Text lineText;

        /// A UI element that appears after lines have finished appearing
        public GameObject continuePrompt;

        /// How quickly to show the text, in seconds per character
        [Tooltip("How quickly to show the text, in seconds per character")]
        public float textSpeed = 0.025f;

        /// The buttons that let the user choose an option
        public List<Button> optionButtons;

        /// Make it possible to temporarily disable the controls when
        /// dialogue is active and to restore them when dialogue ends
        public RectTransform gameControlsContainer;

        void Awake ()
        {
            // Start by hiding the container, line and option buttons
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);

            lineText.gameObject.SetActive (false);

            foreach (var button in optionButtons) {
                button.gameObject.SetActive (false);
            }

            // Hide the continue prompt if it exists
            if (continuePrompt != null)
                continuePrompt.SetActive (false);
        }

        /// Show a line of dialogue, gradually
        public override Dialogue.HandlerExecutionType RunLine (Yarn.Line line, System.Action onComplete)
        {
            StartCoroutine(DoRunLine(line, onComplete));
            return Dialogue.HandlerExecutionType.PauseExecution;
        }

        public IEnumerator DoRunLine(Yarn.Line line, System.Action onComplete) {
            // Show the text
            lineText.gameObject.SetActive (true);

            if (textSpeed > 0.0f) {
                // Display the line one character at a time
                var stringBuilder = new StringBuilder ();

                foreach (char c in line.Text) {
                    stringBuilder.Append (c);
                    lineText.text = stringBuilder.ToString ();
                    yield return new WaitForSeconds (textSpeed);
                }
            } else {
                // Display the line immediately if textSpeed == 0
                lineText.text = line.Text;
            }

            // Show the 'press any key' prompt when done, if we have one
            if (continuePrompt != null)
                continuePrompt.SetActive (true);

            // Wait for any user input
            while (Input.anyKeyDown == false) {
                yield return null;
            }

            // Avoid skipping lines if textSpeed == 0
            yield return new WaitForEndOfFrame();

            // Hide the text and prompt
            lineText.gameObject.SetActive (false);

            if (continuePrompt != null)
                continuePrompt.SetActive (false);
            
            onComplete();

        }

        public override void RunOptions (Yarn.OptionSet optionsCollection, System.Action<int> selectOption) {
            StartCoroutine(DoRunOptions(optionsCollection, selectOption));
        }

        bool waitingForOptionSelection = false;

        /// Show a list of options, and wait for the player to make a selection.
        public  IEnumerator DoRunOptions (Yarn.OptionSet optionsCollection, System.Action<int> selectOption)
        {
            // Do a little bit of safety checking
            if (optionsCollection.Options.Length > optionButtons.Count) {
                Debug.LogWarning("There are more options to present than there are" +
                                 "buttons to present them in. This will cause problems.");
            }

            // Display each option in a button, and make it visible
            int i = 0;

            waitingForOptionSelection = true;
            
            foreach (var optionString in optionsCollection.Options) {
                optionButtons [i].gameObject.SetActive (true);

                // When the button is selected, tell the dialogue about it
                optionButtons [i].onClick.RemoveAllListeners();
                optionButtons [i].onClick.AddListener(() => {
                    waitingForOptionSelection = false;
                    selectOption(optionString.ID);
                });

                optionButtons [i].GetComponentInChildren<Text> ().text = optionString.Line.Text;
                i++;
            }

            // Wait until the chooser has been used and then removed (see SetOption below)
            while (waitingForOptionSelection) {
                yield return null;
            }

            // Hide all the buttons
            foreach (var button in optionButtons) {
                button.gameObject.SetActive (false);
            }
        }

        /// Run an internal command.

        public override Dialogue.HandlerExecutionType RunCommand (Yarn.Command command, System.Action onComplete) {
            StartCoroutine(DoRunCommand(command, onComplete));
            return Dialogue.HandlerExecutionType.ContinueExecution;
        }

        public IEnumerator DoRunCommand (Yarn.Command command, System.Action onComplete)
        {
            // "Perform" the command
            Debug.Log ("Command: " + command.Text);

            yield break;
        }

        /// Called when the dialogue system has started running.
        public override void DialogueStarted ()
        {
            Debug.Log ("Dialogue starting!");

            // Enable the dialogue controls.
            if (dialogueContainer != null)
                dialogueContainer.SetActive(true);

            // Hide the game controls.
            if (gameControlsContainer != null) {
                gameControlsContainer.gameObject.SetActive(false);
            }            
        }

        /// Called when the dialogue system has finished running.
        public override void DialogueComplete ()
        {
            Debug.Log ("Complete!");

            // Hide the dialogue interface.
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);

            // Show the game controls.
            if (gameControlsContainer != null) {
                gameControlsContainer.gameObject.SetActive(true);
            }
        }

    }

}
