using System;
using UnityEngine;
#if ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

/// <summary>
/// A voiceover AudioClip and its associated language
/// </summary>
[Serializable]
public class LanguageToAudioclip {
    public LanguageToAudioclip(string Language, AudioClip AudioClip = null) {
        language = Language;
        audioClip = AudioClip;
    }

    public string language;
    public AudioClip audioClip;
#if ADDRESSABLES
    public AssetReference audioClipAddressable;
#endif
}