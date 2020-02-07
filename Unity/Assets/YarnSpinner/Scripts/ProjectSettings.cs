using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yarn's project wide settings that will automatically be included in a build and not altered after that.
/// </summary>
[System.Serializable]
public class ProjectSettings : ScriptableObject {
    /// <summary>
    /// Project wide available text languages
    /// </summary>
    [SerializeField]
    private List<string> _textProjectLanguages = new List<string>();
    /// <summary>
    /// Project wide available text languages
    /// </summary>
    public static List<string> TextProjectLanguages => Instance._textProjectLanguages;

    /// <summary>
    /// Project wide available audio voice over languages
    /// </summary>
    [SerializeField]
    private List<string> _audioProjectLanguages = new List<string>();
    /// <summary>
    /// Project wide available audio voice over languages
    /// </summary>
    public static List<string> AudioProjectLanguages => Instance._audioProjectLanguages;

    /// <summary>
    /// Path to Yarn's project settings
    /// </summary>
    private static string _settingsPath;

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0051", Justification = "Called from Unity/upon creaton")]
    private void Awake() {
        if (_instance != null && this != _instance) {
            DestroyImmediate(_instance);
        }
        _instance = this;

#if UNITY_EDITOR
        _settingsPath = Application.dataPath + "/../ProjectSettings" + "/YarnProjectSettings.json";
        YarnSettingsHelper.ReadPreferencesFromDisk(this, _settingsPath, Initialize);
#else
        _settingsPath = "YarnProjectSettings";
        var jsonString = Resources.Load<TextAsset>(_settingsPath);
        var test = jsonString.text.ToString();
        if (!string.IsNullOrEmpty(test)) {
            YarnSettingsHelper.ReadJsonFromString(this, test, Initialize);
        }
#endif
    }

    private void Initialize() {
        _textProjectLanguages = new List<string>();
        _audioProjectLanguages = new List<string>();
    }

    private void OnDestroy() {
        SortAudioLanguagesList();
        WriteProjectSettingsToDisk();
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

    /// <summary>
    /// Write current Yarn project settings from memory to disk.
    /// </summary>
    public static void WriteProjectSettingsToDisk() {
        YarnSettingsHelper.WritePreferencesToDisk(Instance, _settingsPath);
    }
}
