using UnityEngine;

namespace Yarn.Unity {
    /// <summary>
    /// Handles playback of voice over audio files from the yarn dialogue system.
    /// </summary>
    public class VoiceOverPlaybackUnity : VoiceOverPlaybackBase {
        [SerializeField]
        private AudioSource _audioSource;

        private void Awake() {
            if (!_audioSource) {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f;
            }
        }

        /// <summary>
        /// Start playback of voice over.
        /// </summary>
        /// <param name="currentLine">The Yarn line currently active.</param>
        /// <param name="voiceOver">The AudioClip accociated with the current Yarn line.</param>
        /// <param name="dialogueUI">The reference to the DialogueUIBehaviour handling this line. Call VoiceOverDuration on this behaviour if you want the UI to wait for audio playback to finish.</param>
        public override void StartLineVoiceOver(Line currentLine, AudioClip voiceOver = null, DialogueUIBehaviour dialogueUI = null) {
            if (!voiceOver) {
                Debug.Log("Playing voice over failed since the given AudioClip was null.", gameObject);
                return;
            }
            if (_audioSource.isPlaying) {
                // TODO: Do this without possible artifats
                _audioSource.Stop();
            }
            _audioSource.PlayOneShot(voiceOver);

            var _audioSourcePlaybackSpeed = Mathf.Abs(_audioSource.pitch);
            var remainingTimeForVoiceOver = voiceOver.length / (_audioSourcePlaybackSpeed > 0 ? _audioSourcePlaybackSpeed : Mathf.Epsilon);
            remainingTimeForVoiceOver = Mathf.Clamp(remainingTimeForVoiceOver, 0, float.MaxValue /*Never show this to Hideo Kojima or he'll use this!*/);

            dialogueUI?.VoiceOverDuration(remainingTimeForVoiceOver);

            return;
        }
    }
}

