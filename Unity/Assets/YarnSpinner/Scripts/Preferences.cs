using UnityEngine;
using System;
using System.Linq;
using System.IO;
using System.Globalization;

[Serializable]
public class Preferences : ScriptableObject {

    public string textLanguage;
    public string audioLanguage;

    private string _preferencesPath;
    private string textLanguageFromDisk;
    private string audioLanguageFromDisk;

    private void Awake() {
        _preferencesPath = Application.persistentDataPath + "/preferences-language.json";
        ReadPreferencesFromDisk();
    }

    private void OnEnable() {
        if (string.IsNullOrEmpty(Cultures.AvailableCulturesNames.FirstOrDefault(element => element == textLanguage))) {
            textLanguage = CultureInfo.CurrentCulture.Name;
        }
        if (string.IsNullOrEmpty(Cultures.AvailableCulturesNames.FirstOrDefault(element => element == audioLanguage))) {
            audioLanguage = CultureInfo.CurrentCulture.Name;
        }
    }

    private void OnDestroy() {
        if (PreferencesChanged()) {
            WritePreferencesToDisk();
        }
    }


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
        if (!string.IsNullOrEmpty(textLanguage)) {
            var matchingTextLanguage = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == textLanguage);
            if (!string.IsNullOrEmpty(matchingTextLanguage)) {
                // Language ID from JSON found in available Cultures so apply
                textLanguage = matchingTextLanguage;
            } else {
                // Language ID from JSON was not found in available Cultures so reset
                textLanguage = CultureInfo.CurrentCulture.Name;
            }
            textLanguageFromDisk = textLanguage;
        }

        // Apply audio language preference from file
        if (!string.IsNullOrEmpty(audioLanguage)) {
            var matchingAudioLanguage = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == audioLanguage);
            if (!string.IsNullOrEmpty(matchingAudioLanguage)) {
                // Language ID from JSON found in available Cultures so apply
                audioLanguage = matchingAudioLanguage;
            } else {
                // Language ID from JSON was not found in available Cultures so reset
                audioLanguage = CultureInfo.CurrentCulture.Name;
            }
            audioLanguageFromDisk = audioLanguage;
        }
    }

    public void WritePreferencesToDisk() {
        string settingsJson = JsonUtility.ToJson(this, true);
        try {
            File.WriteAllText(_preferencesPath, settingsJson);
        } catch (Exception) {
            Debug.LogError("Saving Yarn Spinner preferences to disk failed!");
        }
    }

    private bool PreferencesChanged() {
        if (textLanguage != textLanguageFromDisk) {
            return true;
        }

        if (audioLanguage != audioLanguageFromDisk) {
            return true;
        }

        return false;
    }
}
