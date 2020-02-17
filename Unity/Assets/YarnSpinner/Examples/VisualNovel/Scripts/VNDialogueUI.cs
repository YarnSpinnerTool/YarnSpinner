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
    /// displays dialogue and choices for the visual novel example...
    /// for convenience, this inherits from DialogueUI instead of DialogueUIBehavior
    public class VNDialogueUI : Yarn.Unity.DialogueUI
    {
        /// handles Yarn commands for visual novels
        private VNManager vnManager;

        /// name plate
        public GameObject nameTextContainer;
        public DialogueRunner.StringUnityEvent onNameUpdate;

        /// we need to override RunLine to run DoUpdateName()
        public override Dialogue.HandlerExecutionType RunLine (Yarn.Line line, IDictionary<string,string> strings, System.Action onComplete)
        {
            DoDetectSpeakerName(line, strings);
            return base.RunLine( line, strings, onComplete );
        }

        private void DoDetectSpeakerName(Yarn.Line line, IDictionary<string,string> strings) {
            if (strings.TryGetValue(line.ID, out var text) == false) {
                Debug.LogWarning($"Line {line.ID} doesn't have any localised text.");
                text = line.ID;
                return;
            }

            // extract speaker's name, if any
            string speakerName = "";
            if ( text.Contains(":") ) { // if there's a ":" separator, then identify the first part as a speaker
                var splitLine = text.Split( new char[] {':'}, 2); // but only split once
                speakerName = splitLine[0].Trim();
                strings[line.ID] = splitLine[1].Trim(); // override string table entry with speaker name stripped out
            }
            
            // change dialog nameplate text and, if applicable the BG color
            if ( speakerName.Length > 0 ) {
                nameTextContainer.SetActive(true);
                onNameUpdate?.Invoke(speakerName);

                if ( vnManager == null ) { vnManager = FindObjectOfType<VNManager>(); }

                if ( vnManager != null ) {
                    // update name BG color, if possible
                    if ( vnManager.actorColors.ContainsKey(speakerName) && nameTextContainer.GetComponent<Image>() != null ) {
                        nameTextContainer.GetComponent<Image>().color = vnManager.actorColors[speakerName];
                    }
                    // Highlight actor's sprite (if on-screen) using VNManager, if possible
                    if ( vnManager.actors.ContainsKey(speakerName) ) {
                        vnManager.HighlightSprite( vnManager.actors[speakerName] );
                    }
                }
            } else { // no speaker name found, so hide the nameplate
                nameTextContainer.SetActive(false);
            }
        } 

    }

}
