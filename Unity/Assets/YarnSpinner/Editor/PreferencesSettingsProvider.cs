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

        GUILayout.Label("Language Preferences", EditorStyles.boldLabel);
        _preferences.Update();

        // Text language popup related things
        SerializedProperty textLanguageProp = DrawLanguagePreference(LanguagePreference.TextLanguage);

        // Audio language popup related things
        SerializedProperty audioLanguageProp = DrawLanguagePreference(LanguagePreference.AudioLanguage);

        _preferences.ApplyModifiedProperties();

        // Trigger events in case the preferences have been changed
        if (_textLanguageLastFrame != textLanguageProp.stringValue) {
            Preferences.LanguagePreferencesChanged?.Invoke(this, System.EventArgs.Empty);
        }
        if (_audioLanguageLastFrame != audioLanguageProp.stringValue) {
            Preferences.LanguagePreferencesChanged?.Invoke(this, System.EventArgs.Empty);
        }
    }

    /// <summary>
    /// Draws a language selection popup.
    /// Returns the corresponding SerializedProperty of the drawn language selection popup which be used after ApplyModifiedProperties() to detect changes to the settings.
    /// </summary>
    /// <param name="languagePreference">Determines wheter to draw the Text Language preference or the Audio Language preference.</param>
    private SerializedProperty DrawLanguagePreference(LanguagePreference languagePreference) {
        // Declare and set variables depending on the type of language preference to draw
        List<string> languages = default;
        SerializedProperty preferencesProperty = default;
        string defaultProjectLanguage = default;
        string infoMessageOnEmptyProjectLanguageList = default;
        string languagePopupLabel = default;

        switch (languagePreference) {
            case LanguagePreference.TextLanguage:
                languages = _textLanguages;
                preferencesProperty = _preferences.FindProperty("_textLanguage");
                defaultProjectLanguage = ProjectSettings.TextProjectLanguageDefault;
                infoMessageOnEmptyProjectLanguageList = _emptyTextLanguageMessage;
                languagePopupLabel = "Text Language";
                _textLanguageLastFrame = preferencesProperty.stringValue;
                break;
            case LanguagePreference.AudioLanguage:
                languages = _audioLanguage;
                preferencesProperty = _preferences.FindProperty("_audioLanguage");
                defaultProjectLanguage = ProjectSettings.AudioProjectLanguageDefault;
                infoMessageOnEmptyProjectLanguageList = _emptyAudioLanguageMessage;
                languagePopupLabel = "Audio Language";
                _audioLanguageLastFrame = preferencesProperty.stringValue;
                break;
        }

        // Get currently available languages and determine which the selected language should be
        int selectedLanguageIndex = -1;
        string[] languagesNamesAvailableForSelection = languages.Count > 0 ? languages.ToArray() : System.Array.Empty<string>();
        var selectedLanguage = languagesNamesAvailableForSelection
            .Select((name, index) => new { name, index })
            .FirstOrDefault(element => element.name == preferencesProperty.stringValue);
        if (selectedLanguage != null) {
            selectedLanguageIndex = selectedLanguage.index;
        } else if (!string.IsNullOrEmpty(defaultProjectLanguage)) {
            // Assign default language in case the currently selected language has become invalid
            selectedLanguageIndex = 0;
        }
        string[] languagesDisplayNamesAvailableForSelection = Cultures.LanguageNamesToDisplayNames(languagesNamesAvailableForSelection);
        // Disable popup and show message box in case the project languages have been defined yet
        if (languagesNamesAvailableForSelection.Length == 0) {
            GUI.enabled = false;
            EditorGUILayout.HelpBox(infoMessageOnEmptyProjectLanguageList, MessageType.Info);
        }
        // Draw the actual language popup
        selectedLanguageIndex = EditorGUILayout.Popup(languagePopupLabel, selectedLanguageIndex, languagesDisplayNamesAvailableForSelection);
        // Change/set the language ID
        if (selectedLanguageIndex != -1) {
            preferencesProperty.stringValue = languagesNamesAvailableForSelection[selectedLanguageIndex];
        } else {
            // null the language ID since the index is invalid
            preferencesProperty.stringValue = string.Empty;
        }
        GUI.enabled = true;

        return preferencesProperty;
    }

    /// <summary>
    /// Available types of language preferences.
    /// </summary>
    private enum LanguagePreference {
        TextLanguage,
        AudioLanguage
    }

    // Register the YarnSpinner user preferences in the "Preferences" window
    [SettingsProvider]
    public static SettingsProvider CreatePreferencesSettingsProvider() {
        var provider = new PreferencesSettingsProvider("Preferences/Yarn Spinner", SettingsScope.User);

        provider.keywords = new HashSet<string>(new[] { "Language", "Text", "Audio" });

        return provider;
    }
}
