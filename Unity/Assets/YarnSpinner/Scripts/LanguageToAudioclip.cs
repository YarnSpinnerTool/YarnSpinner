using System;
using UnityEngine;
#if ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

/// <summary>
/// A voice over <see cref="AudioClip"/> and its associated language.
/// </summary>
[Serializable]
public class LanguageToAudioclip {
    public LanguageToAudioclip(string Language, AudioClip AudioClip = null) {
        language = Language;
        audioClip = AudioClip;
    }

    /// <summary>
    /// The language ID (e.g. "en" or "de).
    /// </summary>
    public string language;

    /// <summary>
    /// The <see cref="AudioClip"/> associated with the <see cref="language"/> ID.
    /// </summary>
    public AudioClip audioClip;
#if ADDRESSABLES
    /// <summary>
    /// The <see cref="AudioClip"/> stored as <see cref="AssetReference"/> 
    /// associated with the <see cref="language"/> ID. Needs to be part of an 
    /// Addressable group and will be loaded asynchronously.
    /// </summary>
    public AssetReference audioClipAddressable;
#endif
}