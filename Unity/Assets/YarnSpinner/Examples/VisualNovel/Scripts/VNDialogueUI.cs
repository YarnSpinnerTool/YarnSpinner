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
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Yarn.Unity.Example {
    /// <summary>
    /// displays dialogue and choices for the visual novel example...
    /// for convenience, this inherits from DialogueUI instead of DialogueUIBehavior,
    /// so make sure you read the base class DialogueUI to know what's happening
    /// </summary>
    public class VNDialogueUI : Yarn.Unity.DialogueUI
    {
        /// <summary>handles Yarn commands for visual novels</summary>
        private VNManager vnManager;

        /// <summary>name plate to display the speaker's name; optionally, put a UI Image background on it</summary>
        [Tooltip("displays the speaker's name, should contain some sort of Text component in its hierarchy")]
        public GameObject nameTextContainer;

        [Tooltip("similar to DialogueUI.onLineUpdate, have this fire out to a Text / TextMesh / TextMeshPro")]
        public DialogueRunner.StringUnityEvent onNameUpdate;


        /// <summary>
        /// overrides DialogueUI.RunLine to create temporary string table and run DoDetectSpeakerName()
        /// </summary>
        public override Dialogue.HandlerExecutionType RunLine (Yarn.Line line, IDictionary<string,string> strings, System.Action onComplete)
        {
            // get localized text from the string table
            if (strings.TryGetValue(line.ID, out var text) == false) {
                Debug.LogWarning($"Line {line.ID} doesn't have any localised text.");
                text = line.ID;
            }
            
            // create a temporary string table so we can strip out the speaker's name
            // don't modify the permanent string table because it will break repeating dialogue
            // (if you replay a dialogue line, the speaker name will already be stripped out)
            var modifiedStringTable = new Dictionary<string, string>();
            modifiedStringTable.Add( line.ID, text );

            // pass temporary modified string table into dialogue display, and 
            DoDetectSpeakerName( line, modifiedStringTable, text );

            // return control back to the normal DialogueUI.RunLine
            return base.RunLine( line, modifiedStringTable, onComplete );
        }

        /// <summary>
        /// also strips out the speaker's name, if present, from the string table parameter
        /// </summary>
        private void DoDetectSpeakerName(Yarn.Line line, IDictionary<string,string> strings, string text) {
            // extract speaker's name, if any
            string speakerName = "";
            if ( text.Contains(":") ) { // if there's a ":" separator, then identify the first part as a speaker
                var splitLine = text.Split( new char[] {':'}, 2); // split the line into 2 parts based on the ":" position
                speakerName = splitLine[0].Trim(); // extract speaker name from before the string split
                strings[line.ID] = splitLine[1].Trim(); // MODIFY STRING TABLE (!!!), strip out the speaker name
            }
            
            // change dialog nameplate text and, if applicable the nameplate BG color
            if ( speakerName.Length > 0 ) {
                nameTextContainer.SetActive(true);
                onNameUpdate?.Invoke(speakerName);

                // optional features: change the name plate color or highlight the actor's sprite
                if ( vnManager == null ) { vnManager = GetComponent<VNManager>(); }
                if ( vnManager != null ) {
                    // update name BG color, if possible
                    if ( vnManager.actors.ContainsKey(speakerName) && nameTextContainer.GetComponentInChildren<Image>() != null ) {
                        nameTextContainer.GetComponentInChildren<Image>().color = vnManager.actors[speakerName].actorColor;
                    }
                    // Highlight actor's sprite (if on-screen) using VNManager, if possible
                    if ( vnManager.actors.ContainsKey(speakerName) ) {
                        vnManager.HighlightSprite( vnManager.actors[speakerName].actorImage );
                    }
                }

            } else { // no speaker name found, so hide the nameplate
                nameTextContainer.SetActive(false);
            }
        } 

    }

}
