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
    public void PlayVoiceOver (AudioClip audioClip, System.Action onVoiceoverTriggeredSuccessfully, System.Action onVoiceoverFinish) {
        if (!audioClip) {
            Debug.Log("Playing voice over failed since the given AudioClip was null.", gameObject);
            return;
        }
        _audioSource.PlayOneShot(audioClip);

        if (_audioSource.isPlaying) {
            onVoiceoverTriggeredSuccessfully?.Invoke();
            StopAllCoroutines();
            StartCoroutine(CallVoiceoverFinish(onVoiceoverFinish));
        }
    }

    /// <summary>
    /// Checks the AudioSource playback status and calls onVoiceFinish when finished
    /// </summary>
    /// <param name="onVoiceoverFinish"></param>
    /// <returns></returns>
    private IEnumerator CallVoiceoverFinish (System.Action onVoiceoverFinish) {
        while (_audioSource.isPlaying) {
            yield return null;
        }

        onVoiceoverFinish?.Invoke();
    }
}
