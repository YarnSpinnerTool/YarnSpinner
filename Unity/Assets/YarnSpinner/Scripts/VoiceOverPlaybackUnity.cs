using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    /// Start playback of voiceover file.
    /// </summary>
    /// <param name="audioClip">The voiceover asset file</param>
    /// <param name="onVoiceoverTriggeredSuccessfully">Action to call if playback started successfully</param>
    /// <param name="onVoiceoverFinish">Action to call when playback finished</param>
    public void PlayVoiceOver (AudioClip audioClip, System.Action<float> voiceOverDuration) {
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
