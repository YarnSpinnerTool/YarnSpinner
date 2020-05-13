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

    private const string _emptyTextLanguageMessage = "To set a preference for text language, add one or more languages to 'Project Setting->Yarn Spinner' first.";
    private const string _emptyAudioLanguageMessage = "To set a preference for audio language, add one or more languages to 'Project Setting->Yarn Spinner' first.";
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
        var textLanguagesNamesAvailableForSelection = _textLanguages.Count > 0 ? _textLanguages.ToArray() : System.Array.Empty<string>();
        var selectedTextLanguage = textLanguagesNamesAvailableForSelection
            .Select((name, index) => new { name, index })
            .FirstOrDefault(element => element.name == textLanguageProp.stringValue);
        if (selectedTextLanguage != null) {
            selectedTextLanguageIndex = selectedTextLanguage.index;
        }
        var textLanguagesDisplayNamesAvailableForSelection = Cultures.LanguageNamesToDisplayNames(textLanguagesNamesAvailableForSelection);
        // Draw the actual text language popup
        if (textLanguagesNamesAvailableForSelection.Length == 0) {
            GUI.enabled = false;
            EditorGUILayout.HelpBox(_emptyTextLanguageMessage, MessageType.Info);
        }
        selectedTextLanguageIndex = EditorGUILayout.Popup("Text Language", selectedTextLanguageIndex, textLanguagesDisplayNamesAvailableForSelection);
        // Change/set the text language ID (or don't touch the setting if the index is unusable)
        if (selectedTextLanguageIndex != -1) {
            textLanguageProp.stringValue = textLanguagesNamesAvailableForSelection[selectedTextLanguageIndex];
        }
        GUI.enabled = true;

        // Audio language popup related things
        var selectedAudioLanguageIndex = -1;
        var audioLanguageProp = _preferences.FindProperty("_audioLanguage");
        _audioLanguageLastFrame = audioLanguageProp.stringValue;
        var audioLanguagesNamesAvailableForSelection = _audioLanguage.Count > 0 ? _audioLanguage.ToArray() : System.Array.Empty<string>();
        var selectedAudioLanguage = audioLanguagesNamesAvailableForSelection
            .Select((name, index) => new { name, index })
            .FirstOrDefault(element => element.name == audioLanguageProp.stringValue);
        if (selectedAudioLanguage != null) {
            selectedAudioLanguageIndex = selectedAudioLanguage.index;
        }
        var audioLanguagesDisplayNamesAvailableForSelection = Cultures.LanguageNamesToDisplayNames(audioLanguagesNamesAvailableForSelection);
        if (audioLanguagesNamesAvailableForSelection.Length == 0) {
            GUI.enabled = false;
            EditorGUILayout.HelpBox(_emptyAudioLanguageMessage, MessageType.Info);
        }
        // Draw the actual audio language popup
        selectedAudioLanguageIndex = EditorGUILayout.Popup("Audio Language", selectedAudioLanguageIndex, audioLanguagesDisplayNamesAvailableForSelection);
        // Change/set the audio language ID (or don't touch the setting if the index is unusable)
        if (selectedAudioLanguageIndex != -1) {
            audioLanguageProp.stringValue = audioLanguagesNamesAvailableForSelection[selectedAudioLanguageIndex];
        }
        GUI.enabled = true;

        _preferences.ApplyModifiedProperties();

        // Trigger events in case the preferences have been changed
        if (_textLanguageLastFrame != textLanguageProp.stringValue) {
            Preferences.LanguagePreferencesChanged?.Invoke(this, System.EventArgs.Empty);
        }
        if (_audioLanguageLastFrame != audioLanguageProp.stringValue) {
            Preferences.LanguagePreferencesChanged?.Invoke(this, System.EventArgs.Empty);
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
