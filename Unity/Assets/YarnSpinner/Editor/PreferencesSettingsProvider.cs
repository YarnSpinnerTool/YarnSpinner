﻿using System.Collections.Generic;
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

    public override void OnActivate(string searchContext, VisualElement rootElement) {
        _preferences = new SerializedObject(ScriptableObject.CreateInstance<Preferences>());
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
        var selectedTextLanguage = Cultures.AvailableCulturesNames
            .Select((name, index) => new { name, index })
            .FirstOrDefault(element => element.name == textLanguageProp.stringValue);
        if (selectedTextLanguage != null) {
            selectedTextLanguageIndex = selectedTextLanguage.index;
        }
        // Draw the actual text language popup
        selectedTextLanguageIndex = EditorGUILayout.Popup("Text Language", selectedTextLanguageIndex, Cultures.AvailableCulturesDisplayNames);
        // Change/set the text language ID (to system's default if the index is unusable)
        textLanguageProp.stringValue = selectedTextLanguageIndex != -1
            ? Cultures.AvailableCulturesNames[selectedTextLanguageIndex]
            : System.Globalization.CultureInfo.CurrentCulture.Name;


        // Audio language popup related things
        var selectedAudioLanguageIndex = -1;
        var audioLanguageProp = _preferences.FindProperty("_audioLanguage");
        var selectedAudioLanguage = Cultures.AvailableCulturesNames
            .Select((name, index) => new { name, index })
            .FirstOrDefault(element => element.name == audioLanguageProp.stringValue);
        if (selectedAudioLanguage != null) {
            selectedAudioLanguageIndex = selectedAudioLanguage.index;
        }
        // Draw the actual audio language popup
        selectedAudioLanguageIndex = EditorGUILayout.Popup("Audio Language", selectedAudioLanguageIndex, Cultures.AvailableCulturesDisplayNames);
        // Change/set the audio language ID (to system's default if the index is unusable)
        audioLanguageProp.stringValue = selectedAudioLanguageIndex != -1
            ? Cultures.AvailableCulturesNames[selectedAudioLanguageIndex]
            : System.Globalization.CultureInfo.CurrentCulture.Name;


        _preferences.ApplyModifiedProperties();
    }

    // Register the YarnSpinner user preferences in the "Preferences" window
    [SettingsProvider]
    public static SettingsProvider CreatePreferencesSettingsProvider() {
        var provider = new PreferencesSettingsProvider("Preferences/Yarn Spinner", SettingsScope.User);

        provider.keywords = new HashSet<string>(new[] { "Language", "Text", "Audio" });

        return provider;
    }
}