using UnityEngine;
using System;
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
        ReadPreferencesFromDisk(false);
    }

    private void Initialize () {
        _textLanguage = null;
        _audioLanguage = null;
        WritePreferencesToDisk(true, false);
    }

    private void OnEnable() {
        if (string.IsNullOrEmpty(Array.Find(Cultures.AvailableCulturesNames, element => element == _textLanguage)) || !ProjectSettings.TextProjectLanguages.Contains(_textLanguage)) {
            _textLanguage = GetDefaultTextLanguage();
        }
        if (string.IsNullOrEmpty(Array.Find(Cultures.AvailableCulturesNames, element => element == _audioLanguage)) || !ProjectSettings.AudioProjectLanguages.Contains(_audioLanguage)) {
            _audioLanguage = GetDefaultAudioLanguage();
        }
    }

    private void OnDestroy() {
        WritePreferencesToDisk(false, false);
    }

    /// <summary>
    /// Returns true if settings have been changed and returns false if no settings have been changed.
    /// </summary>
    /// <returns></returns>
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
    /// /// <param name="useJson">If true, settings will be read from JSON file (default). If false, settings will be read from Unity's PlayerPrefs.</param>
    private void ReadPreferencesFromDisk(bool useJson = true) {
        if (useJson) {
            YarnSettingsHelper.ReadPreferencesFromDisk(this, _preferencesPath, Initialize);
        } else {
            _textLanguage = PlayerPrefs.GetString("Yarn-TextLanguage");
            _audioLanguage = PlayerPrefs.GetString("Yarn-AudioLanguage");
        }

        // Apply text language preference from file
        if (!string.IsNullOrEmpty(_textLanguage)) {
            // Keep the value read from disk to be able to tell if this class has been modified during runtime
            _textLanguageFromDisk = Array.Find(Cultures.AvailableCulturesNames, element => element == _textLanguage);
            if (string.IsNullOrEmpty(_textLanguageFromDisk)) {
                // Language ID from JSON was not found in available Cultures so try to reset with current culture or the project's default language
                _textLanguage = ProjectSettings.TextProjectLanguages.Contains(CultureInfo.CurrentCulture.Name) ? CultureInfo.CurrentCulture.Name : ProjectSettings.TextProjectLanguageDefault;
            }
        }

        // Apply audio language preference from file
        if (!string.IsNullOrEmpty(_audioLanguage)) {
            // Keep the value read from disk to be able to tell if this class has been modified during runtime
            _audioLanguageFromDisk = Array.Find(Cultures.AvailableCulturesNames, element => element == _audioLanguage);
            if (string.IsNullOrEmpty(_audioLanguageFromDisk)) {
                // Language ID from JSON was not found in available Cultures so try to reset with current culture or the project's default language
                _audioLanguage = ProjectSettings.AudioProjectLanguages.Contains(CultureInfo.CurrentCulture.Name) ? CultureInfo.CurrentCulture.Name : ProjectSettings.AudioProjectLanguageDefault;
            }
        }
    }

    /// <summary>
    /// Write the preferences to disk. Will be stored outside of the project in the user settings directory of the OS.
    /// </summary>
    /// <param name="force">Force writing the settings to disk (true) or only write to disk if settings have been changed (false).</param>
    /// <param name="useJson">If true, settings will be written as JSON (default). If false, settings will be written to Unity's PlayerPrefs.</param>
    private static void WritePreferencesToDisk(bool force = false, bool useJson = true) {
        if (!Instance.PreferencesChanged() && !force) {
            return;
        }

        if (useJson) {
            YarnSettingsHelper.WritePreferencesToDisk(Instance, Instance._preferencesPath);
        } else {
            PlayerPrefs.SetString("Yarn-TextLanguage", Instance._textLanguage);
            PlayerPrefs.SetString("Yarn-AudioLanguage", Instance._audioLanguage);
        }
    }

    private static string GetDefaultTextLanguage() {
        return ProjectSettings.TextProjectLanguages.Contains(CultureInfo.CurrentCulture.Name) ? CultureInfo.CurrentCulture.Name : ProjectSettings.TextProjectLanguageDefault;
    }

    private static string GetDefaultAudioLanguage() {
        return ProjectSettings.AudioProjectLanguages.Contains(CultureInfo.CurrentCulture.Name) ? CultureInfo.CurrentCulture.Name : ProjectSettings.AudioProjectLanguageDefault;
    }
    #endregion

    #region Public Methods

    #endregion
}
