using UnityEngine;
using System;
using System.Linq;
using System.Globalization;

/// <summary>
/// Yarn preferences made by the user that should not be stored in a project or in a build.
/// </summary>
[Serializable]
public class Preferences : ScriptableObject {
    #region Properties
    /// <summary>
    /// Raised when the language preferences have been changed
    /// </summary>
    public static EventHandler LanguagePreferencesChanged;

    /// <summary>
    /// The text language preferred by the user
    /// </summary>
    [SerializeField]
    private string _textLanguage;

    /// <summary>
    /// The audio language preferred by the user
    /// </summary>
    [SerializeField]
    private string _audioLanguage;

    /// <summary>
    /// The path to store the user preferences
    /// </summary>
    private string _preferencesPath;

    /// <summary>
    /// The text language preference that was read from disk.
    /// Used to detect changes to reduce writing to disk.
    /// </summary>
    private string _textLanguageFromDisk;

    /// <summary>
    /// The audio language preference that was read from disk.
    /// Used to detect changes to reduce writing to disk.
    /// </summary>
    private string _audioLanguageFromDisk;

    /// <summary>
    /// Instance of this class (Singleton design pattern)
    /// </summary>
    private static Preferences _instance;
    #endregion

    #region Accessors
    /// <summary>
    /// Makes sure that there's always an instance of this
    /// class alive upon access.
    /// </summary>
    private static Preferences Instance {
        get {
            if (!_instance) {
                // Calls Awake() implicitly
                _instance = CreateInstance<Preferences>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// The text language preferred by the user. Changes will
    /// be written to disk during exit and ending playmode.
    /// </summary>
    public static string TextLanguage {
        get => Instance._textLanguage;
        set {
            if (value != Instance._textLanguage) {
                Instance._textLanguage = value;
                LanguagePreferencesChanged?.Invoke(Instance, EventArgs.Empty);
            } else {
                Instance._textLanguage = value;
            }
        }
    }

    /// <summary>
    /// The audio language preferred by the user. Changes will
    /// be written to disk during exit and ending playmode.
    /// </summary>
    public static string AudioLanguage {
        get => Instance._audioLanguage;
        set {
            if (value != Instance._audioLanguage) {
                Instance._audioLanguage = value;
                LanguagePreferencesChanged?.Invoke(Instance, EventArgs.Empty);
            } else {
                Instance._audioLanguage = value;
            }
        }
    }
    #endregion

    #region Private Methods
    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0051", Justification = "Called implicitly by Unity upon creation")]
    private void Awake() {
        if (_instance != null && this != _instance) {
            DestroyImmediate(_instance);
        }
        _instance = this;
        _preferencesPath = Application.persistentDataPath + "/preferences-language.json";
        ReadPreferencesFromDisk();
    }

    private void Initialize () {
        _textLanguage = null;
        _audioLanguage = null;
    }

    private void OnEnable() {
        if (string.IsNullOrEmpty(Cultures.AvailableCulturesNames.FirstOrDefault(element => element == _textLanguage))) {
            _textLanguage = CultureInfo.CurrentCulture.Name;
        }
        if (string.IsNullOrEmpty(Cultures.AvailableCulturesNames.FirstOrDefault(element => element == _audioLanguage))) {
            _audioLanguage = CultureInfo.CurrentCulture.Name;
        }
    }

    private void OnDestroy() {
        WritePreferencesToDisk();
    }

    private bool PreferencesChanged() {
        if (_textLanguage != _textLanguageFromDisk) {
            return true;
        }

        if (_audioLanguage != _audioLanguageFromDisk) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Read the user's language preferences from disk.
    /// </summary>
    private void ReadPreferencesFromDisk() {
        YarnSettingsHelper.ReadPreferencesFromDisk(this, _preferencesPath, Initialize);

        // Apply text language preference from file
        if (!string.IsNullOrEmpty(_textLanguage)) {
            // Keep the value read from disk to be able to tell if this class has been modified during runtime
            _textLanguageFromDisk = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == _textLanguage);
            if (string.IsNullOrEmpty(_textLanguageFromDisk)) {
                // Language ID from JSON was not found in available Cultures so reset
                _textLanguage = CultureInfo.CurrentCulture.Name;
            }
        }

        // Apply audio language preference from file
        if (!string.IsNullOrEmpty(_audioLanguage)) {
            // Keep the value read from disk to be able to tell if this class has been modified during runtime
            _audioLanguageFromDisk = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == _audioLanguage);
            if (string.IsNullOrEmpty(_audioLanguageFromDisk)) {
                // Language ID from JSON was not found in available Cultures so reset
                _audioLanguage = CultureInfo.CurrentCulture.Name;
            }
        }
    }

    private static void WritePreferencesToDisk() {
        if (Instance.PreferencesChanged()) {
            YarnSettingsHelper.WritePreferencesToDisk(Instance, Instance._preferencesPath);
        }
    }
    #endregion

    #region Public Methods

    #endregion
}
