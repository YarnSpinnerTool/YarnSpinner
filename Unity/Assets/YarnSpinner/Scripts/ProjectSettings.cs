using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ProjectSettings : ScriptableObject {
    [SerializeField]
    private List<string> _textProjectLanguages = new List<string>();
    public static List<string> TextProjectLanguages => Instance._textProjectLanguages;

    [SerializeField]
    private List<string> _audioProjectLanguages = new List<string>();
    public static List<string> AudioProjectLanguages => Instance._audioProjectLanguages;

    public static string SettingsPath { get; private set; }

    /// <summary>
    /// Instance of this class (Singleton design pattern)
    /// </summary>
    private static ProjectSettings _instance;

    /// <summary>
    /// Makes sure that there's always an instance of this 
    /// class alive upon access.
    /// </summary>
    private static ProjectSettings Instance {
        get {
            if (!_instance) {
                // Calls Awake() implicitly
                _instance = CreateInstance<ProjectSettings>();
            }
            return _instance;
        }
    }


    private void Awake() {
        if (_instance != null && this != _instance) {
            DestroyImmediate(_instance);
        }
        _instance = this;

#if UNITY_EDITOR
        SettingsPath = Application.dataPath + "/../ProjectSettings" + "/YarnProjectSettings.json";
        YarnSettingsHelper.ReadPreferencesFromDisk(this, SettingsPath, Initialize);
#else
        SettingsPath = "YarnProjectSettings";
        var jsonString = Resources.Load<TextAsset>(SettingsPath);
        var test = jsonString.text.ToString();
        if (!string.IsNullOrEmpty(test)) {
            YarnSettingsHelper.ReadJsonFromString(this, test, Initialize);
        }
#endif
    }

    private void OnDestroy() {
        SortAudioLanguagesList();
        WriteProjectSettingsToDisk();
    }

    public static void WriteProjectSettingsToDisk() {
        YarnSettingsHelper.WritePreferencesToDisk(Instance, SettingsPath);
    }

    private void Initialize () {
        _textProjectLanguages = new List<string>();
        _audioProjectLanguages = new List<string>();
    }

    /// <summary>
    /// Sort the audio languages list to match the text languages list
    /// </summary>
    private void SortAudioLanguagesList() {
        var audioLanguagesSorted = new List<string>();
        foreach (var textLanguage in _textProjectLanguages) {
            if (_audioProjectLanguages.Contains(textLanguage)) {
                audioLanguagesSorted.Add(textLanguage);
            }
        }
        _audioProjectLanguages = audioLanguagesSorted;
    }
}
