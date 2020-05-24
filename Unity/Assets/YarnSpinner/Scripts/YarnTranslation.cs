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
    public string languageName;
    public TextAsset text;
}
