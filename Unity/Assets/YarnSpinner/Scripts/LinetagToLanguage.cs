using System;

/// <summary>
/// A linetag and it's corresponding <see cref="UnityEngine.AudioClip"/> per available language for voice over dialogues.
/// </summary>
[Serializable]
public class LinetagToLanguage {
    public LinetagToLanguage(string Linetag) {
        linetag = Linetag;
    }

    /// <summary>
    /// The linetag/string ID of a Yarn line.
    /// </summary>
    public string linetag;

    /// <summary>
    /// The <see cref="UnityEngine.AudioClip"/>s associated with this <see cref="linetag"/> per available language.
    /// </summary>
    public LanguageToAudioclip[] languageToAudioclip = Array.Empty<LanguageToAudioclip>();
}