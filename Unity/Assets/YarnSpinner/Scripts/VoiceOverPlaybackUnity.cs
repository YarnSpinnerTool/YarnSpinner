using System;
using System.Collections;
using UnityEngine;

namespace Yarn.Unity {
    /// <summary>
    /// Handles playback of voice over <see cref="AudioClip"/>s referenced on <see cref="YarnProgram"/>s.
    /// </summary>
    public class VoiceOverPlaybackUnity : DialogueViewBase {
        /// <summary>
        /// The fade out time when <see cref="FinishCurrentLine"/> is called.
        /// </summary>
        public float fadeOutTimeOnLineFinish = 0.05f;

        [SerializeField]
        private AudioSource audioSource;

        /// <summary>
        /// When true, the <see cref="DialogueRunner"/> has signaled to finish the current line
        /// asap.
        /// </summary>
        private bool finishCurrentLine = false;

        private void Awake() {
            if (!audioSource) {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
            }
        }

        /// <summary>
        /// Start playback of the associated voice over <see cref="AudioClip"/> of the given <see cref="LocalizedLine"/>.
        /// </summary>
        /// <param name="dialogueLine"></param>
        /// <returns></returns>
        protected override IEnumerator RunLine(LocalizedLine dialogueLine) {
            finishCurrentLine = false;

            // Get the localized voice over audio clip
            var voiceOverClip = dialogueLine.VoiceOverLocalized;

            if (!voiceOverClip) {
                Debug.Log("Playing voice over failed since the AudioClip of the voice over audio language or the base language was null.", gameObject);
                yield break;
            }
            if (audioSource.isPlaying) {
                // Usually, this shouldn't happen because the DialogueRunner finishes and ends a line first
                audioSource.Stop();
            }
            audioSource.PlayOneShot(voiceOverClip);

            while (audioSource.isPlaying && !finishCurrentLine) {
                yield return null;
            }

            // Fade out voice over clip
            if (audioSource.isPlaying && finishCurrentLine) {
                float lerpPosition = 0f;
                float volumeFadeStart = audioSource.volume;
                while (audioSource.volume != 0) {
                    lerpPosition += Time.unscaledDeltaTime / fadeOutTimeOnLineFinish;
                    audioSource.volume = Mathf.Lerp(volumeFadeStart, 0, lerpPosition);
                    yield return null;
                }
                audioSource.Stop();
                audioSource.volume = volumeFadeStart;
            } else {
                audioSource.Stop();
            }
        }

        /// <summary>
        /// Instruct playback of the current <see cref="LocalizedLine"/>'s voice over <see cref="AudioClip"/> to stop asap.
        /// Applies fade out defined by <see cref="fadeOutTimeOnLineFinish"/>.
        /// </summary>
        protected override void FinishCurrentLine() {
            finishCurrentLine = true;
        }

        protected override IEnumerator EndCurrentLine() {
            // Avoid skipping lines if textSpeed == 0
            yield return new WaitForEndOfFrame();
        }

        /// <summary>
        /// YarnSpinner's implementation of voice over playback does nothing when presenting an option.
        /// </summary>
        /// <param name="dialogueOptions"></param>
        /// <param name="onOptionSelected"></param>
        public override void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected) {
            // Do nothing
        }

        /// <summary>
        /// YarnSpinner's implementation of voice over playback does nothing when all Views finished a line.
        /// </summary>
        /// <param name="dialogueOptions"></param>
        /// <param name="onOptionSelected"></param>
        internal override void OnFinishedLineOnAllViews() {
            // Do nothing
        }
    }
}

