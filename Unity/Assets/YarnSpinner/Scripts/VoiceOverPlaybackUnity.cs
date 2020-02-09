using UnityEngine;
using Yarn;

/// <summary>
/// Handles playback of voice over audio files from the yarn dialogue system.
/// </summary>
public class VoiceOverPlaybackUnity : MonoBehaviour
{
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
    /// <param name="audioClip">The AudioClip accociated with the current Yarn line.</param>
    /// <param name="voiceOverDuration">The action to call should the DialogueUI wait for this AudioClip to finish playback.</param>
    public void PlayVoiceOver (Line currentLine, AudioClip audioClip, System.Action<float> voiceOverDuration) {
        if (!audioClip) {
            Debug.Log("Playing voice over failed since the given AudioClip was null.", gameObject);
            return;
        }
        _audioSource.PlayOneShot(audioClip);

        var _audioSourcePlaybackSpeed = Mathf.Abs(_audioSource.pitch);
        var remainingTimeForVoiceOver = audioClip.length / (_audioSourcePlaybackSpeed > 0 ? _audioSourcePlaybackSpeed : Mathf.Epsilon);
        remainingTimeForVoiceOver = Mathf.Clamp(remainingTimeForVoiceOver, 0, float.MaxValue /*Never show this to Hideo Kojima or he'll use this!*/);

        voiceOverDuration?.Invoke(remainingTimeForVoiceOver);

        return;
    }
}
