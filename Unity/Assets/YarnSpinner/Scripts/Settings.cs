using UnityEngine;
using System;
using System.Linq;
using System.IO;
using System.Globalization;

[CreateAssetMenu(fileName = "YarnSettings", menuName = "Yarn")]
[Serializable]
public class Settings : ScriptableObject {

    public string textLanguage;
    public string audioLanguage;
    
    private string _settingsPath;


    private void OnEnable() {
        if (string.IsNullOrEmpty(Cultures.AvailableCulturesNames.FirstOrDefault(element => element == textLanguage))) {
            textLanguage = CultureInfo.CurrentCulture.Name;
        }
        if (string.IsNullOrEmpty(Cultures.AvailableCulturesNames.FirstOrDefault(element => element == audioLanguage))) {
            audioLanguage = CultureInfo.CurrentCulture.Name;
        }

        _settingsPath = Application.persistentDataPath + "/options-language.json";
    }

    public void ReadSettingsFromDisk() {
        // Check file's existence
        bool fileExists = File.Exists(_settingsPath);
        if (!fileExists) {
            Debug.LogError("File doesn't exist. Cannot load");
            return;
        }

        // Load file into memory
        string jsonString;
        try {
            jsonString = File.ReadAllText(_settingsPath);
        } catch (Exception) {
            Debug.LogError("Error loading language options from JSON.");
            return;
        }

        // Parse json to Setting SO
        Settings settings = CreateInstance<Settings>();
        try {
            JsonUtility.FromJsonOverwrite(jsonString, settings);
        } catch (Exception) {
            Debug.LogError("Error parsing language options from JSON.");
            return;
        }

        // Apply text language setting from file
        if (settings != null && !string.IsNullOrEmpty(settings.textLanguage)) {
            var matchingTextLanguage = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == settings.textLanguage);
            if (!string.IsNullOrEmpty(matchingTextLanguage)) {
                textLanguage = matchingTextLanguage;
            }
        }

        // Apply audio language setting from file
        if (settings != null && !string.IsNullOrEmpty(settings.audioLanguage)) {
            var matchingAudioLanguage = Cultures.AvailableCulturesNames.FirstOrDefault(element => element == settings.audioLanguage);
            if (!string.IsNullOrEmpty(matchingAudioLanguage)) {
                audioLanguage = matchingAudioLanguage;
            }
        }
    }

    public void WriteSettingsToDisk() {
        string settingsJson = JsonUtility.ToJson(this, true);
        try {
            File.WriteAllText(_settingsPath, settingsJson);
        } catch (Exception) {
            Debug.LogError("Saving options to disk failed!");
        }
    }
}
