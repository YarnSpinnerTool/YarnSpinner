using UnityEngine;

namespace Yarn.Unity {
    /// <summary>
    /// Handles voice over playback
    /// </summary>
    public abstract class VoiceOverPlaybackBase : MonoBehaviour {
        /// <summary>
        /// Start playback of a voice over line and optionally request the UI to wait until playback is finished.
        /// </summary>
        /// <param name="currentLine">The yarn line we want to playback a voice over file for.</param>
        /// <param name="voiceOver">The associated voice over AudioClip. Can be null if external middlewares are used.</param>
        /// <param name="dialogueUI"></param>
        public abstract void StartLineVoiceOver(Yarn.Line currentLine, AudioClip voiceOver = null, DialogueViewBase dialogueUI = null);
    }
}
