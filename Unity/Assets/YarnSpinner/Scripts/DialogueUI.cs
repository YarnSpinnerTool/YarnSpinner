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
using System;

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

        // When true, the user has indicated that they want to proceed to
        // the next line.
        private bool userRequestedNextLine = false;

        // The method that we should call when the user has chosen an
        // option. Externally provided by the DialogueRunner.
        private System.Action<int> currentOptionSelectionHandler;

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

        public enum LineAudioType {
            NoAudio,
            UnityAudioSource,
            Middleware
        }

        public LineAudioType lineAudioType;

        /// <summary>
        /// Event sending the current linetag as string for processing in external applications like audio middlewares,
        /// an action to call when the linetag was successfully resolved and voiceover playback started and
        /// an action to call when the voiceover playback finished.
        /// </summary>
        [System.Serializable]
        public class MiddlewareStringUnityEvent : UnityEngine.Events.UnityEvent<string, System.Action, System.Action> { }

        /// <summary>
        /// Is called when a dialogue line is run
        /// </summary>
#pragma warning disable 0649
        [SerializeField] MiddlewareStringUnityEvent onLineStartAudioMiddleware;
#pragma warning restore 0649

        /// <summary>
        /// Is called when a dialogue line is interrupted
        /// </summary>
#pragma warning disable 0649
        [SerializeField] DialogueRunner.StringUnityEvent onLineCancelAudioMiddleware;
#pragma warning restore 0649

        private System.Action audioMiddlewareReportedPlaybackStart;
        private System.Action audioMiddlewareReportedPlaybackEnd;
        public bool IsLineAudioPlaying {get; private set;}
        private AudioSource lineAudioSource;

        public bool autoAdvanceAfterAudioComplete = true;
        
        void Awake ()
        {
            // Start by hiding the container
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);

            foreach (var button in optionButtons) {
                button.gameObject.SetActive (false);
            }

            audioMiddlewareReportedPlaybackEnd = LineAudioBeganPlayback;
            audioMiddlewareReportedPlaybackEnd = LineAudioCompletedPlayback;
        }

        public override Dialogue.HandlerExecutionType RunLine (Yarn.Line line, ILineLocalisationProvider localisation, System.Action onComplete)
        {
            // Start displaying the line; it will call onComplete later
            // which will tell the dialogue to continue
            StartCoroutine(DoRunLine(line, localisation, onComplete));
            return Dialogue.HandlerExecutionType.PauseExecution;
        }

        /// Show a line of dialogue, gradually        
        private IEnumerator DoRunLine(Yarn.Line line, ILineLocalisationProvider localisationProvider, System.Action onComplete) {
            onLineStart?.Invoke();

            // Clear a bunch of state in preparation for delivering this line
            userRequestedNextLine = false;
            IsLineAudioPlaying = false;

            // Tracks whether we have attempted to play audio for a line.
            // (If we aren't playing audio, or if we're playing via Unity
            // audio and didn't find an AudioClip for it, this will be
            // false.)
            bool requestedAudioPlayback = false;

            // The final text we'll be showing for this line.
            string text = localisationProvider.GetLocalisedTextForLine(line);

            if (text == null) {
                Debug.LogWarning($"Line {line.ID} doesn't have any localised text.");
                text = line.ID;
            }
            
            // Attempt to start audio playback for this line
            if (lineAudioType == LineAudioType.UnityAudioSource) {

                // Find the audio clip for this line and play it via an
                // AudioSource

                var audioClip = localisationProvider.GetLocalisedAudioClipForLine(line);

                if (audioClip == null) {
                    Debug.Log($"No voice over for line {line.ID} found for current localisation.", gameObject);
                } else {                    
                    requestedAudioPlayback = true;
                    StartCoroutine(DoPlayLineAudio(audioClip));
                }     
            } else if (lineAudioType == LineAudioType.Middleware) {
                
                // Delegate the audio playback to some middleware. It will
                // call the two delegates we provide to indicate start and
                // end.
                requestedAudioPlayback = true;
                onLineStartAudioMiddleware.Invoke(line.ID, 
                    audioMiddlewareReportedPlaybackStart, 
                    audioMiddlewareReportedPlaybackEnd);                
            }
            
            if (textSpeed > 0.0f) {
                // Display the line one character at a time
                var stringBuilder = new StringBuilder ();

                foreach (char c in text) {
                    stringBuilder.Append (c);
                    onLineUpdate?.Invoke(stringBuilder.ToString ());
                    if (userRequestedNextLine) {
                        // We've requested a skip of the entire line.
                        // Display all of the text immediately.
                        onLineUpdate?.Invoke(text);
                        break;
                    }
                    yield return new WaitForSeconds (textSpeed);
                }
            } else {
                // Display the entire line immediately if textSpeed <= 0
                onLineUpdate?.Invoke(text);
            }

            // We're now waiting for the line to finish. This can happen in
            // one of two ways: the audio can end, or the user can manually
            // signal to move on. We only move to the next audio line when:
            // 1. we successfully told something to start playing audio,
            //    and
            // 2. autoAdvanceAfterAudioComplete is true
            userRequestedNextLine = false;

            // Indicate to the rest of the game that the line has finished being delivered
            onLineFinishDisplaying?.Invoke();

            if (autoAdvanceAfterAudioComplete && requestedAudioPlayback) {
                // We have indicated that we want to wait until the audio
                // has finished for this line, and then tell the dialogue
                // system that we're ready to move on.
                //
                // We still want the user to be able to interrupt the line
                // and skip on to the dialogue, so we wait for either audio
                // to stop playing or for the user to request the next line.
                while (IsLineAudioPlaying == true && userRequestedNextLine == false) {
                    yield return null;
                }

                if (IsLineAudioPlaying == true && userRequestedNextLine == true) {
                    // The user requested the next line, but audio is still
                    // playing. We need to cancel the audio. The Unity
                    // audio source will handle this for us, but if we
                    // delegated the playback to middleware, we need to
                    // signal it.
                    if (lineAudioType == LineAudioType.Middleware) {
                        onLineCancelAudioMiddleware.Invoke(line.ID);
                    }
                }
                
                // Clear the audio is playing flag in case this was an interruption
                IsLineAudioPlaying = false;
            } else {
                // We're not using the duration of the audio to control how
                // long this line runs for. We'll need to wait for the
                // 'user requested next line' signal instead.
                while (userRequestedNextLine == false) {
                    yield return null;
                }
            }
            
            // Avoid skipping lines if textSpeed == 0
            yield return new WaitForEndOfFrame();

            // Hide the text and prompt
            onLineEnd?.Invoke();

            onComplete();

        }

        public override void RunOptions (Yarn.OptionSet optionsCollection, ILineLocalisationProvider localisationProvider, System.Action<int> selectOption) {
            StartCoroutine(DoRunOptions(optionsCollection, localisationProvider, selectOption));
        }

        /// Show a list of options, and wait for the player to make a
        /// selection.
        public  IEnumerator DoRunOptions (Yarn.OptionSet optionsCollection, ILineLocalisationProvider localisationProvider, System.Action<int> selectOption)
        {
            // Do a little bit of safety checking
            if (optionsCollection.Options.Length > optionButtons.Count) {
                Debug.LogWarning("There are more options to present than there are" +
                                 "buttons to present them in. This will cause problems.");
            }

            // Display each option in a button, and make it visible
            int i = 0;

            waitingForOptionSelection = true;

            currentOptionSelectionHandler = selectOption;
            
            foreach (var optionString in optionsCollection.Options) {
                optionButtons [i].gameObject.SetActive (true);

                // When the button is selected, tell the dialogue about it
                optionButtons [i].onClick.RemoveAllListeners();
                optionButtons [i].onClick.AddListener(() => SelectOption(optionString.ID));

                var optionText = localisationProvider.GetLocalisedTextForLine(optionString.Line);

                if (optionText == null) {
                    Debug.LogWarning($"Option {optionString.Line.ID} doesn't have any localised text");
                    optionText = optionString.Line.ID;
                }

                var unityText = optionButtons [i].GetComponentInChildren<Text> ();
                if (unityText != null) {
                    unityText.text = optionText;
                }

                var textMeshProText = optionButtons [i].GetComponentInChildren<TMPro.TMP_Text> ();
                if (textMeshProText != null) {
                    textMeshProText.text = optionText;
                }

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

        /// Run a command.
        public override Dialogue.HandlerExecutionType RunCommand (Yarn.Command command, System.Action onComplete) {
            // Dispatch this command via the 'On Command' handler.
            onCommand?.Invoke(command.Text);

            // Signal to the DialogueRunner that it should continue executing.
            return Dialogue.HandlerExecutionType.ContinueExecution;
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
            userRequestedNextLine = true;
        }

        public void SelectOption(int index) {
            if (waitingForOptionSelection == false) {
                Debug.LogWarning("An option was selected, but the dialogue UI was not expecting it.");
                return;
            }
            waitingForOptionSelection = false;
            currentOptionSelectionHandler?.Invoke(index);
        }

        private IEnumerator DoPlayLineAudio(AudioClip clip) {
            if (lineAudioSource == null) {
                var source = GetComponent<AudioSource>();
                if (source == null) {
                    // Create a new 2D sound source
                    source = gameObject.AddComponent<AudioSource>();
                    source.spatialBlend = 0f;
                }
                lineAudioSource = source;
            }

            if (lineAudioSource.isPlaying) {
                lineAudioSource.Stop();                
            }

            lineAudioSource.PlayOneShot(clip);

            if (lineAudioSource.isPlaying) {
                LineAudioBeganPlayback();

                while (lineAudioSource.isPlaying) {
                    yield return null;
                }

                LineAudioCompletedPlayback();
            }          
            

        }

        private void LineAudioBeganPlayback()
        {
            IsLineAudioPlaying = true;
        }

        private void LineAudioCompletedPlayback()
        {
            IsLineAudioPlaying = false;
        }
    }

}
