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
    private ReorderableList _projetLanguagesReorderableList;
    private ReorderableList _textLanguagesReorderableList;
    private ReorderableList _audioLanguagesReorderableList;
    private int _projetLanguagesListIndex;
    private int _textLanguagesListIndex;
    private int _audioLanguagesListIndex;

    public static SerializedObject ProjectSettings {
        get {
            if (_projectSettings == null) {
                _projectSettings = GetProjectSettings();
            }
            return _projectSettings;
        }
    }

    public override void OnActivate(string searchContext, VisualElement rootElement) {
        _projectSettings = GetProjectSettings();

        // Initialize the language lists
        _projetLanguagesReorderableList = new ReorderableList(_projectSettings, _projectSettings.FindProperty("_projectLanguages"), true, true, false, true);
        _textLanguagesReorderableList = new ReorderableList(_projectSettings, _projectSettings.FindProperty("_textProjectLanguages"), true, true, false, true);
        _audioLanguagesReorderableList = new ReorderableList(_projectSettings, _projectSettings.FindProperty("_audioProjectLanguages"), true, true, false, true);
        // Add labels to the lists
        _projetLanguagesReorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Languages available for this project");
        };
        _textLanguagesReorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Languages available for this project");
        };
        _audioLanguagesReorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Languages available for this project");
        };
        // How an element of the lists should be drawn
        _projetLanguagesReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var languageId = _projetLanguagesReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            var displayName = Cultures.LanguageNamesToDisplayNames(languageId.stringValue);
            rect.y += 2;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), displayName);
        };
        _textLanguagesReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var languageId = _textLanguagesReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            var displayName = Cultures.LanguageNamesToDisplayNames(languageId.stringValue);
            rect.y += 2;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), displayName);
        };
        _audioLanguagesReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var languageId = _audioLanguagesReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            var displayName = Cultures.LanguageNamesToDisplayNames(languageId.stringValue);
            rect.y += 2;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), displayName);
        };
    }

    public override void OnGUI(string searchContext) {
        if (_projectSettings == null || _projectSettings.targetObject == null) {
            return;
        }
        _projectSettings.Update();

        GUILayout.Label("Project Languages", EditorStyles.boldLabel);
        // Project languages (List of languages available in this project)
        var projectLanguagesProp = _projectSettings.FindProperty("_projectLanguages");
        //EditorGUILayout.PropertyField(projectLanguagesProp);
        var projectLanguages = (_projectSettings.targetObject as ProjectSettings)._projectLanguages;
        var remainingProjectLanguages = Cultures.AvailableCulturesNames.Except(projectLanguages).ToArray();
        var remainingProjectLanguagesDisplayNames = Cultures.LanguageNamesToDisplayNames(remainingProjectLanguages);
        // Button and Dropdown List for adding a language
        GUILayout.BeginHorizontal();
        if (remainingProjectLanguages.Length < 0) {
            GUI.enabled = false;
            GUILayout.Button("No more languages left to add");
            GUI.enabled = true;
        } else {
            if (GUILayout.Button("Add Language to Project Languages")) {
                projectLanguagesProp.InsertArrayElementAtIndex(projectLanguagesProp.arraySize);
                projectLanguagesProp.GetArrayElementAtIndex(projectLanguagesProp.arraySize - 1).stringValue = remainingProjectLanguages[_projetLanguagesListIndex];
                _projetLanguagesListIndex = 0;
            }
        }
        _projetLanguagesListIndex = EditorGUILayout.Popup(_projetLanguagesListIndex, remainingProjectLanguagesDisplayNames);
        GUILayout.EndHorizontal();
        // Project Language List
        _projetLanguagesReorderableList.DoLayoutList();

        GUILayout.Space(20);
        GUILayout.Label("Text Languages", EditorStyles.boldLabel);
        // Text languages (sub-selection from available project languages)
        var textLanguagesProp = _projectSettings.FindProperty("_textProjectLanguages");
        //EditorGUILayout.PropertyField(textLanguagesProp);
        var textLanguages = (_projectSettings.targetObject as ProjectSettings)._textProjectLanguages;
        var remainingTextLanguages = projectLanguages.Except(textLanguages).ToArray();
        var remainingTextLanguagesDisplayNames = Cultures.LanguageNamesToDisplayNames(remainingTextLanguages);
        // Button and Dropdown List for adding a language
        GUILayout.BeginHorizontal();
        if (remainingTextLanguages.Length < 1) {
            GUI.enabled = false;
            GUILayout.Button("No more available Project Languages");
            GUI.enabled = true;
        } else {
            if (GUILayout.Button("Add Language to Text Languages")) {
                textLanguagesProp.InsertArrayElementAtIndex(textLanguagesProp.arraySize);
                textLanguagesProp.GetArrayElementAtIndex(textLanguagesProp.arraySize - 1).stringValue = remainingTextLanguages[_textLanguagesListIndex];
                _textLanguagesListIndex = 0;
            }
        }
        _textLanguagesListIndex = EditorGUILayout.Popup(_textLanguagesListIndex, remainingTextLanguagesDisplayNames);
        GUILayout.EndHorizontal();

        // Cleanup Text Language List from languages that have been removed from the Project Languages
        for (int i = textLanguages.Count - 1; i >= 0; i--) {
            string language = (string)textLanguages[i];
            if (!projectLanguages.Contains(language)) {
                textLanguagesProp.DeleteArrayElementAtIndex(i);
            }
        }

        // Text Language List
        _textLanguagesReorderableList.DoLayoutList();

        GUILayout.Space(20);
        GUILayout.Label("Audio Languages", EditorStyles.boldLabel);
        // Audio languages (sub-selection from available project languages)
        var audioLanguagesProp = _projectSettings.FindProperty("_audioProjectLanguages");
        //EditorGUILayout.PropertyField(_projectSettings.FindProperty("_audioProjectLanguages"));
        var audioLanguages = (_projectSettings.targetObject as ProjectSettings)._audioProjectLanguages;
        var remainingAudioLanguages = projectLanguages.Except(audioLanguages).ToArray();
        var remainingAudioLanguagesDisplayNames = Cultures.LanguageNamesToDisplayNames(remainingAudioLanguages);
        // Button and Dropdown List for adding a language
        GUILayout.BeginHorizontal();
        if (remainingAudioLanguages.Length < 1) {
            //GUI.enabled = false;
            GUILayout.Button("No more available Project Languages", EditorStyles.helpBox);
            //GUI.enabled = true;
        } else {
            if (GUILayout.Button("Add Language to Audio Languages")) {
                audioLanguagesProp.InsertArrayElementAtIndex(audioLanguagesProp.arraySize);
                audioLanguagesProp.GetArrayElementAtIndex(audioLanguagesProp.arraySize - 1).stringValue = remainingAudioLanguages[_audioLanguagesListIndex];
                _audioLanguagesListIndex = 0;
            }
        }
        _audioLanguagesListIndex = EditorGUILayout.Popup(_audioLanguagesListIndex, remainingAudioLanguagesDisplayNames);
        GUILayout.EndHorizontal();

        // Cleanup Audio Language List from languages that have been removed from the Project Languages
        for (int i = audioLanguages.Count - 1; i >= 0; i--) {
            string language = (string)audioLanguages[i];
            if (!projectLanguages.Contains(language)) {
                audioLanguagesProp.DeleteArrayElementAtIndex(i);
            }
        }

        // Draw Audio Language List
        _audioLanguagesReorderableList.DoLayoutList();


        _projectSettings.ApplyModifiedProperties();
    }

    // Register YarnSpinner's project settings in the "Project Settings" window
    [SettingsProvider]
    public static SettingsProvider CreatePreferencesSettingsProvider() {
        var provider = new ProjectSettingsProvider("Project/Yarn Spinner", SettingsScope.Project);

        provider.keywords = new HashSet<string>(new[] { "Language", "Text", "Audio" });

        return provider;
    }

    private static SerializedObject GetProjectSettings() {
        // Handle Yarn's project settings asset
        // 1. Try to locate the asset
        var asset = AssetDatabase.FindAssets("t:ProjectSettings");
        string _pathToYarnProjectSettingsAsset;
        if (asset.Length > 0) {
            // 2.a Asset found, cache path for OnGUI calls
            _pathToYarnProjectSettingsAsset = AssetDatabase.GUIDToAssetPath(asset[0]);
        } else {
            // 2.b No asset found so create and cache path
            _pathToYarnProjectSettingsAsset = AssetDatabase.GenerateUniqueAssetPath("Assets/YarnProjectSettings.asset");
            var settingsObject = ScriptableObject.CreateInstance<ProjectSettings>();
            AssetDatabase.CreateAsset(settingsObject, _pathToYarnProjectSettingsAsset);
        }
        // Load the asset
        return new SerializedObject(AssetDatabase.LoadAssetAtPath<ProjectSettings>(_pathToYarnProjectSettingsAsset));
    }
}
