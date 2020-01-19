using System;

/// <summary>
/// A linetag and a coresponding AudioClip per available language for voiceover dialogues.
/// </summary>
[Serializable]
public class LinetagToLanguage {
    public LinetagToLanguage(string Linetag) {
        linetag = Linetag;
    }

    public string linetag;
    public LanguageToAudioclip[] languageToAudioclip = new LanguageToAudioclip[0];
}