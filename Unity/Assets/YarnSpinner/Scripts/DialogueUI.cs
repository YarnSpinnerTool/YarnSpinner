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

namespace Yarn.Unity {
    /// Displays dialogue lines to the player, and sends
    /// user choices back to the dialogue system.

    public class DialogueUI : Yarn.Unity.DialogueUIBehaviour
    {

        /// The object that contains the dialogue and the options.
        /** This object will be enabled when conversation starts, and
         * disabled when it ends.
         */
        public GameObject dialogueContainer;

        /// How quickly to show the text, in seconds per character
        [Tooltip("How quickly to show the text, in seconds per character")]
        public float textSpeed = 0.025f;

        /// The buttons that let the user choose an option
        public List<Button> optionButtons;

        // When true, the DialogueRunner is waiting for the user to
        // indicate that they want to proceed to the next line.
        private bool waitingForLineContinue = false;

        // When true, the DialogueRunner is waiting for the user to press
        // one of the option buttons.
        private bool waitingForOptionSelection = false;      

        public UnityEngine.Events.UnityEvent onDialogueStart;

        public UnityEngine.Events.UnityEvent onDialogueEnd;  

        public UnityEngine.Events.UnityEvent onLineStart;
        public UnityEngine.Events.UnityEvent onLineFinishDisplaying;
        public DialogueRunner.StringUnityEvent onLineUpdate;
        public UnityEngine.Events.UnityEvent onLineEnd;

        public UnityEngine.Events.UnityEvent onOptionsStart;
        public UnityEngine.Events.UnityEvent onOptionsEnd;

        public DialogueRunner.StringUnityEvent onCommand;
        
        void Awake ()
        {
            // Start by hiding the container
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);

            foreach (var button in optionButtons) {
                button.gameObject.SetActive (false);
            }
        }

        public override Dialogue.HandlerExecutionType RunLine (Yarn.Line line, IDictionary<string,string> strings, System.Action onComplete)
        {
            // Start displaying the line; it will call onComplete later
            // which will tell the dialogue to continue
            StartCoroutine(DoRunLine(line, strings, onComplete));
            return Dialogue.HandlerExecutionType.PauseExecution;
        }

        /// Show a line of dialogue, gradually        
        private IEnumerator DoRunLine(Yarn.Line line, IDictionary<string,string> strings, System.Action onComplete) {
            
            onLineStart?.Invoke();

            if (strings.TryGetValue(line.ID, out var text) == false) {
                Debug.LogWarning($"Line {line.ID} doesn't have any localised text.");
                text = line.ID;
            }

            if (textSpeed > 0.0f) {
                // Display the line one character at a time
                var stringBuilder = new StringBuilder ();

                foreach (char c in text) {
                    stringBuilder.Append (c);
                    onLineUpdate?.Invoke(stringBuilder.ToString ());
                    yield return new WaitForSeconds (textSpeed);
                }
            } else {
                // Display the entire line immediately if textSpeed <= 0
                onLineUpdate?.Invoke(text);
            }

            waitingForLineContinue = true;

            onLineFinishDisplaying?.Invoke();

            // Wait for any user input
            while (waitingForLineContinue) {
                yield return null;
            }

            // Avoid skipping lines if textSpeed == 0
            yield return new WaitForEndOfFrame();

            // Hide the text and prompt
            onLineEnd?.Invoke();

            onComplete();

        }

        public override void RunOptions (Yarn.OptionSet optionsCollection, IDictionary<string,string> strings, System.Action<int> selectOption) {
            StartCoroutine(DoRunOptions(optionsCollection, strings, selectOption));
        }

        /// Show a list of options, and wait for the player to make a
        /// selection.
        public  IEnumerator DoRunOptions (Yarn.OptionSet optionsCollection, IDictionary<string,string> strings, System.Action<int> selectOption)
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

                if (strings.TryGetValue(optionString.Line.ID, out var optionText) == false) {
                    Debug.LogWarning($"Option {optionString.Line.ID} doesn't have any localised text");
                    optionText = optionString.Line.ID;
                }

                optionButtons [i].GetComponentInChildren<Text> ().text = optionText;

                i++;
            }

            onOptionsStart?.Invoke();

            // Wait until the chooser has been used and then removed 
            while (waitingForOptionSelection) {
                yield return null;
            }

            
            // Hide all the buttons
            foreach (var button in optionButtons) {
                button.gameObject.SetActive (false);
            }

            onOptionsEnd?.Invoke();

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
            // Enable the dialogue controls.
            if (dialogueContainer != null)
                dialogueContainer.SetActive(true);

            onDialogueStart?.Invoke();            
        }

        /// Called when the dialogue system has finished running.
        public override void DialogueComplete ()
        {
            onDialogueEnd?.Invoke();

            // Hide the dialogue interface.
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);
            
        }

        public void MarkLineComplete() {
            if (waitingForLineContinue == false) {
                Debug.LogWarning($"{nameof(MarkLineComplete)} was called, " + 
                    $"but {nameof(DialogueRunner)} wasn't waiting for a line " + 
                    "to be marked as read.");
                return;
            }

            waitingForLineContinue = false;
        }

    }

}
