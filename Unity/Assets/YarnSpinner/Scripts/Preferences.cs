using UnityEngine;
using System;
using System.Linq;
using System.IO;
using System.Globalization;

[Serializable]
public class Preferences : ScriptableObject {
    #region Properties
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
        set => Instance._textLanguage = value;
    }

    /// <summary>
    /// The audio language preferred by the user. Changes will 
    /// be written to disk during exit and ending playmode.
    /// </summary>
    public static string AudioLanguage {
        get => Instance._audioLanguage;
        set => Instance._audioLanguage = value;
    }
    #endregion

    #region Private Methods
    private void Awake() {
        if (_instance != null && this != _instance) {
            DestroyImmediate(_instance);
        }
        _instance = this;
        _preferencesPath = Application.persistentDataPath + "/preferences-language.json";
        ReadPreferencesFromDisk();
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
        if (PreferencesChanged()) {
            WritePreferencesToDisk();
        }
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
    #endregion

    #region Public Methods
    /// <summary>
    /// Read the user's language preferences from disk.
    /// </summary>
    public void ReadPreferencesFromDisk() {
        // Check file's existence
        bool fileExists = File.Exists(_preferencesPath);
        if (!fileExists) {
            // Wen don't throw an error since during OnEnable() all values will be initialized with 
            // the system's default and create a new file once this class get's out of scope
            Debug.Log("No previous Yarn Spinner preferences have been found.");
            return;
        }

        // Load file into memory
        string jsonString;
        try {
            jsonString = File.ReadAllText(_preferencesPath);
        } catch (Exception) {
            // No big deal since we'll initialize all values during OnEnable()
            Debug.Log("Error loading Yarn Spinner preferences from JSON.");
            return;
        }

        // Parse json to *this* ScriptableObject
        try {
            JsonUtility.FromJsonOverwrite(jsonString, this);
        } catch (Exception) {
            // No big deal since we'll initialize all values during OnEnable()
            Debug.Log("Error parsing Yarn Spinner preferences from JSON.");
            return;
        }

        // Apply text language preference from file
        if (!string.IsNullOrEmpty(_textLanguage)) {
            var matchingTextLanguage = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == _textLanguage);
            _textLanguageFromDisk = matchingTextLanguage;
            if (!string.IsNullOrEmpty(matchingTextLanguage)) {
                // Language ID from JSON found in available Cultures so apply
                _textLanguage = matchingTextLanguage;
            } else {
                // Language ID from JSON was not found in available Cultures so reset
                _textLanguage = CultureInfo.CurrentCulture.Name;
            }
        }

        // Apply audio language preference from file
        if (!string.IsNullOrEmpty(_audioLanguage)) {
            var matchingAudioLanguage = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == _audioLanguage);
            _audioLanguageFromDisk = matchingAudioLanguage;
            if (!string.IsNullOrEmpty(matchingAudioLanguage)) {
                // Language ID from JSON found in available Cultures so apply
                _audioLanguage = matchingAudioLanguage;
            } else {
                // Language ID from JSON was not found in available Cultures so reset
                _audioLanguage = CultureInfo.CurrentCulture.Name;
            }
        }
    }

    /// <summary>
    /// Save the user's language preferences to disk.
    /// </summary>
    public void WritePreferencesToDisk() {
        string settingsJson = JsonUtility.ToJson(this, true);
        try {
            File.WriteAllText(_preferencesPath, settingsJson);
        } catch (Exception) {
            Debug.LogError("Saving Yarn Spinner preferences to disk failed!");
        }
    }
    #endregion

}
