using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if UNITY_2018
using UnityEngine.Experimental.UIElements;
#endif
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#endif

/// <summary>
/// Yarn-related user preferences shown in the "Preferences" window
/// </summary>
class PreferencesSettingsProvider : SettingsProvider {
    public PreferencesSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }

    private SerializedObject _preferences;
    private List<string> _textLanguages = new List<string>();
    private List<string> _audioLanguage = new List<string>();
    private string _textLanguageLastFrame;
    private string _audioLanguageLastFrame;

    public override void OnActivate(string searchContext, VisualElement rootElement) {
        _preferences = new SerializedObject(ScriptableObject.CreateInstance<Preferences>());
        _textLanguages = ProjectSettings.TextProjectLanguages;
        _audioLanguage = ProjectSettings.AudioProjectLanguages;
        _textLanguageLastFrame = Preferences.TextLanguage;
        _audioLanguageLastFrame = Preferences.AudioLanguage;
    }

    public override void OnDeactivate() {
        if (_preferences != null) {
            Object.DestroyImmediate(_preferences.targetObject);
        }
    }

    public override void OnGUI(string searchContext) {
        if (_preferences == null || _preferences.targetObject == null) {
            return;
        }

        _preferences.Update();

        // Text language popup related things
        var selectedTextLanguageIndex = -1;
        var textLanguageProp = _preferences.FindProperty("_textLanguage");
        _textLanguageLastFrame = textLanguageProp.stringValue;
        var textLanguagesNamesAvailableForSelection = _textLanguages.Count > 0 ? _textLanguages.ToArray() : Cultures.AvailableCulturesNames;
        var selectedTextLanguage = textLanguagesNamesAvailableForSelection
            .Select((name, index) => new { name, index })
            .FirstOrDefault(element => element.name == textLanguageProp.stringValue);
        if (selectedTextLanguage != null) {
            selectedTextLanguageIndex = selectedTextLanguage.index;
        }
        var textLanguagesDisplayNamesAvailableForSelection = Cultures.LanguageNamesToDisplayNames(textLanguagesNamesAvailableForSelection);
        // Draw the actual text language popup
        selectedTextLanguageIndex = EditorGUILayout.Popup("Text Language", selectedTextLanguageIndex, textLanguagesDisplayNamesAvailableForSelection);
        // Change/set the text language ID (to system's default if the index is unusable)
        textLanguageProp.stringValue = selectedTextLanguageIndex != -1
            ? textLanguagesNamesAvailableForSelection[selectedTextLanguageIndex]
            : System.Globalization.CultureInfo.CurrentCulture.Name;


        // Audio language popup related things
        var selectedAudioLanguageIndex = -1;
        var audioLanguageProp = _preferences.FindProperty("_audioLanguage");
        _audioLanguageLastFrame = audioLanguageProp.stringValue;
        var audioLanguagesNamesAvailableForSelection = _audioLanguage.Count > 0 ? _audioLanguage.ToArray() : Cultures.AvailableCulturesNames;
        var selectedAudioLanguage = audioLanguagesNamesAvailableForSelection
            .Select((name, index) => new { name, index })
            .FirstOrDefault(element => element.name == audioLanguageProp.stringValue);
        if (selectedAudioLanguage != null) {
            selectedAudioLanguageIndex = selectedAudioLanguage.index;
        }
        var audioLanguagesDisplayNamesAvailableForSelection = Cultures.LanguageNamesToDisplayNames(audioLanguagesNamesAvailableForSelection);
        // Draw the actual audio language popup
        selectedAudioLanguageIndex = EditorGUILayout.Popup("Audio Language", selectedAudioLanguageIndex, audioLanguagesDisplayNamesAvailableForSelection);
        // Change/set the audio language ID (to system's default if the index is unusable)
        audioLanguageProp.stringValue = selectedAudioLanguageIndex != -1
            ? audioLanguagesNamesAvailableForSelection[selectedAudioLanguageIndex]
            : System.Globalization.CultureInfo.CurrentCulture.Name;


        _preferences.ApplyModifiedProperties();

        if (_textLanguageLastFrame != textLanguageProp.stringValue) {
            Preferences.LanguagePreferencesChanged?.Invoke(this, new System.EventArgs());
        }
        if (_audioLanguageLastFrame != audioLanguageProp.stringValue) {
            Preferences.LanguagePreferencesChanged?.Invoke(this, new System.EventArgs());
        }
    }

    // Register the YarnSpinner user preferences in the "Preferences" window
    [SettingsProvider]
    public static SettingsProvider CreatePreferencesSettingsProvider() {
        var provider = new PreferencesSettingsProvider("Preferences/Yarn Spinner", SettingsScope.User);

        provider.keywords = new HashSet<string>(new[] { "Language", "Text", "Audio" });

        return provider;
    }
}
