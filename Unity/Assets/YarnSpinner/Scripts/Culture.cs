using System;

/// <summary>
/// Holds information about a language.
/// </summary>
[Serializable]
public struct Culture {
    /// <summary>
    /// The unique language ID used to identify a language as RFC 4646.
    /// Will be "de-CH" for "German (Switzerland)".
    /// Use this for storing settings or identifying a language.
    /// </summary>
    public string Name;

    /// <summary>
    /// The display name of a language.
    /// Will be "German (Switzerland)" for "de-CH".
    /// Use this value to present the language in an English UI.
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// The languages name as called in the language itself.
    /// Will be "Deutsch (Schweiz) for "de-CH".
    /// Use this to present the language in-game so people can find their native language.
    /// </summary>
    public string NativeName;
}
