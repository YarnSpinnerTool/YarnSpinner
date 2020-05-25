using System;
using UnityEngine;

/// <summary>
/// Represent a .yarn file in a different language.
/// </summary>
[Serializable]
public class YarnTranslation
{
    public YarnTranslation(string LanguageName, TextAsset Text = null) {
        languageName = LanguageName;
        text = Text;
    }

    /// <summary>
    /// Name of the language of this <see cref="YarnTranslation"/> in RFC 4646.
    /// </summary>
    public string languageName;

    /// <summary>
    /// The csv string table containing the translated text.
    /// </summary>
    public TextAsset text;
}
