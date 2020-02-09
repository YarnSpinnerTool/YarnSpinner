using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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

    private static SerializedObject _projectSettings;
    private ReorderableList _textLanguagesReorderableList;
    private int _textLanguagesListIndex;

    public override void OnActivate(string searchContext, VisualElement rootElement) {
        _projectSettings = new SerializedObject(ScriptableObject.CreateInstance<ProjectSettings>());
        var textLanguages = _projectSettings.FindProperty("_textProjectLanguages");
        var audioLanguages = _projectSettings.FindProperty("_audioProjectLanguages");

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

        _projectSettings.ApplyModifiedProperties();
    }

    // Register YarnSpinner's project settings in the "Project Settings" window
    [SettingsProvider]
    public static SettingsProvider CreatePreferencesSettingsProvider() {
        var provider = new ProjectSettingsProvider("Project/Yarn Spinner", SettingsScope.Project);

        provider.keywords = new HashSet<string>(new[] { "Language", "Text", "Audio" });

        return provider;
    }
}
