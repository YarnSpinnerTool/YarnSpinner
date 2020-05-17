using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEditorInternal;
using UnityEngine;
#if UNITY_2018
using UnityEngine.Experimental.UIElements;
#endif
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#endif

/// <summary>
/// Yarn-related project settings shown in the "Project Settings" window
/// </summary>
class ProjectSettingsProvider : SettingsProvider {
    public ProjectSettingsProvider(string path, SettingsScope scope = SettingsScope.Project) : base(path, scope) { }

    // Variables used for the project-wide settings
    private const string buttonTextDeleteAllDirectReferences = "Delete all direct AudioClip references";
    private const string buttonTextDeleteAllAddressableReferences = "Delete all addressable AudioClip references";
    private static SerializedObject _projectSettings;
    private ReorderableList _textLanguagesReorderableList;
    private int _textLanguagesListIndex;
    
    // Variables used for the user's settings (also called "Preferences")
    private static SerializedObject _preferences;
    private const string _emptyTextLanguageMessage = "To set a preference for text language, add one or more languages to 'Project Setting->Yarn Spinner' first.";
    private const string _emptyAudioLanguageMessage = "To set a preference for audio language, add one or more languages to 'Project Setting->Yarn Spinner' first.";
    private List<string> _textLanguages = new List<string>();
    private List<string> _audioLanguage = new List<string>();
    private string _textLanguageLastFrame;
    private string _audioLanguageLastFrame;


    public override void OnActivate(string searchContext, VisualElement rootElement) {
        // Initialize project settings variables
        _projectSettings = new SerializedObject(ScriptableObject.CreateInstance<ProjectSettings>());
        var textLanguages = _projectSettings.FindProperty("_textProjectLanguages");
        var audioLanguages = _projectSettings.FindProperty("_audioProjectLanguages");

        // Initialize user settings variables (preference variables)
        _preferences = new SerializedObject(ScriptableObject.CreateInstance<Preferences>());
        _textLanguages = ProjectSettings.TextProjectLanguages;
        _audioLanguage = ProjectSettings.AudioProjectLanguages;
        _textLanguageLastFrame = Preferences.TextLanguage;
        _audioLanguageLastFrame = Preferences.AudioLanguage;

        // Initialize visual representation of the language lists
        _textLanguagesReorderableList = new ReorderableList(_projectSettings, textLanguages, true, true, false, true);
        // Add labels to the lists
        // Show available text languages to the left and available audio languages to the right
        _textLanguagesReorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width * 0.65f, EditorGUIUtility.singleLineHeight), "Text Languages");
            EditorGUI.LabelField(new Rect(rect.width * 0.65f, rect.y, rect.width * 0.75f, EditorGUIUtility.singleLineHeight), "Audio Languages");
        };
        // How an element of the lists should be drawn
        // Text languages will be drawn left as a label with their display name (-> "English")
        // Audio languages will be drawn right as a bool/toggle field indicating their use (-> true/false)
        // This communicates visually that for adding a voice over language a coresponding text language must exist already
        _textLanguagesReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var languageId = _textLanguagesReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            var displayName = Cultures.LanguageNamesToDisplayNames(languageId.stringValue);
            rect.y += 2;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width * 0.7f, EditorGUIUtility.singleLineHeight), displayName);
            var textLanguageOnAudio = ProjectSettings.AudioProjectLanguages.Contains(languageId.stringValue);
            var audioBool = EditorGUI.Toggle(new Rect(rect.width * 0.7f, rect.y, rect.width * 0.3f, EditorGUIUtility.singleLineHeight), textLanguageOnAudio);
            if (audioBool != textLanguageOnAudio) {
                if (audioBool) {
                    audioLanguages.InsertArrayElementAtIndex(audioLanguages.arraySize);
                    audioLanguages.GetArrayElementAtIndex(audioLanguages.arraySize-1).stringValue = languageId.stringValue;
                } else {
                    var audiolanguageIndex = ProjectSettings.AudioProjectLanguages.IndexOf(languageId.stringValue);
                    audioLanguages.DeleteArrayElementAtIndex(audiolanguageIndex);
                }
            }
        };
    }

    public override void OnDeactivate() {
        if (_projectSettings != null) {
            Object.DestroyImmediate(_projectSettings.targetObject);
        }
        if (_preferences != null) {
            Object.DestroyImmediate(_preferences.targetObject);
        }
    }

    public override void OnGUI(string searchContext) {
        if (_projectSettings == null || _projectSettings.targetObject == null) {
            return;
        }
        _projectSettings.Update();

        GUILayout.Label("Project Languages", EditorStyles.boldLabel);
        // Text languages
        var textLanguagesProp = _projectSettings.FindProperty("_textProjectLanguages");
        var textLanguages = ProjectSettings.TextProjectLanguages;
        var remainingTextLanguages = Cultures.AvailableCulturesNames.Except(textLanguages).ToArray();
        var remainingTextLanguagesDisplayNames = Cultures.LanguageNamesToDisplayNames(remainingTextLanguages);
        // Button and Dropdown List for adding a language
        GUILayout.BeginHorizontal();
        if (remainingTextLanguages.Length < 1) {
            GUI.enabled = false;
            GUILayout.Button("No more available Project Languages");
            GUI.enabled = true;
        } else {
            if (GUILayout.Button("Add language to project")) {
                textLanguagesProp.InsertArrayElementAtIndex(textLanguagesProp.arraySize);
                textLanguagesProp.GetArrayElementAtIndex(textLanguagesProp.arraySize - 1).stringValue = remainingTextLanguages[_textLanguagesListIndex];
                _textLanguagesListIndex = 0;
            }
        }
        _textLanguagesListIndex = EditorGUILayout.Popup(_textLanguagesListIndex, remainingTextLanguagesDisplayNames);
        GUILayout.EndHorizontal();

        // Text Language List
        _textLanguagesReorderableList.DoLayoutList();
        
        // Audio languages (sub-selection from available text languages)
        var audioLanguagesProp = _projectSettings.FindProperty("_audioProjectLanguages");
        var audioLanguages = ProjectSettings.AudioProjectLanguages;

        // Cleanup Audio Language List from languages that have been removed from the Project Languages
        for (int i = audioLanguages.Count - 1; i >= 0; i--) {
            string language = (string)audioLanguages[i];
            if (!textLanguages.Contains(language)) {
                audioLanguagesProp.DeleteArrayElementAtIndex(i);
            }
        }

#if ADDRESSABLES
        GUILayout.Label("Voice Over Asset Handling", EditorStyles.boldLabel);
        var addressableVoiceOverAudioClipsProp = _projectSettings.FindProperty("_addressableVoiceOverAudioClips");
        EditorGUILayout.PropertyField(addressableVoiceOverAudioClipsProp, new GUIContent("Use Addressables"));

        GUILayout.BeginHorizontal();
        GUI.enabled = addressableVoiceOverAudioClipsProp.boolValue;
        if (GUILayout.Button(buttonTextDeleteAllDirectReferences)) {
            RemoveVoiceOverReferences(true);
        }
        GUI.enabled = !addressableVoiceOverAudioClipsProp.boolValue;
        if (GUILayout.Button(buttonTextDeleteAllAddressableReferences)) {
            RemoveVoiceOverReferences(false);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
#endif

        _projectSettings.ApplyModifiedProperties();

        // User's language preferences
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
    /// Draws a language selection popup used for showing a user's language preference.
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

#if ADDRESSABLES
    /// <summary>
    /// Remove all voice over audio clip references or addressable references on all yarn assets.
    /// </summary>
    /// <param name="removeDirectReferences">True if direct audio clip references should be deleted and false if Addressable references should be deleted.</param>
    private static void RemoveVoiceOverReferences(bool removeDirectReferences) {
        if (removeDirectReferences) {
            Debug.Log("Removing all direct AudioClip references on all yarn assets!");
        } else {
            Debug.Log("Removing all Adressable references on all yarn assets!");
        }

        foreach (var yarnProgram in AssetDatabase.FindAssets("t:YarnProgram")) {
            var yarnImporter = ScriptedImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(yarnProgram)) as YarnImporter;
            yarnImporter.RemoveAllVoiceOverReferences(removeDirectReferences);
            EditorUtility.SetDirty(yarnImporter);
            yarnImporter.SaveAndReimport();
        }
    }
#endif

    /// <summary>
    /// Register YarnSpinner's UI for project settings and user preferences in the "Project Settings" window.
    /// The user preferences should be declaed in SettingsScope.User but to avoid confusion by having 
    /// settings windows in two different places we present the UIs of both in a single window.
    /// </summary>
    /// <returns></returns>
    [SettingsProvider]
    public static SettingsProvider CreatePreferencesSettingsProvider() {
        var provider = new ProjectSettingsProvider("Project/Yarn Spinner", SettingsScope.Project);

        provider.keywords = new HashSet<string>(new[] { "Language", "Text", "Audio" });

        return provider;
    }
}
