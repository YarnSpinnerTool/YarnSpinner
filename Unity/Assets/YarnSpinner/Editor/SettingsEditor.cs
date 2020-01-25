using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomEditor(typeof(Settings))][CanEditMultipleObjects]
public class SettingsEditor : Editor {
    int selectedTextLanguageIndex;
    int selectedAudioLanguageIndex;

    public override void OnInspectorGUI() {
        serializedObject.Update();

        // Draw text language popup
        var textLanguageProp = serializedObject.FindProperty("textLanguage");
        var selectedTextLanguage = Cultures.AvailableCulturesNames.Select((name, index) => new { name, index }).FirstOrDefault(element => element.name == textLanguageProp.stringValue);
        if (selectedTextLanguage != null) {
            selectedTextLanguageIndex = selectedTextLanguage.index;
        }
        selectedTextLanguageIndex = EditorGUILayout.Popup("Text Language", selectedTextLanguageIndex, Cultures.AvailableCulturesDisplayNames);

        textLanguageProp.stringValue = selectedTextLanguageIndex != -1
            ? Cultures.AvailableCulturesNames[selectedTextLanguageIndex]
            : System.Globalization.CultureInfo.CurrentCulture.Name;

        // Draw audio language popup
        var audioLanguageProp = serializedObject.FindProperty("audioLanguage");
        var selectedAudioLanguage = Cultures.AvailableCulturesNames.Select((name, index) => new { name, index }).FirstOrDefault(element => element.name == audioLanguageProp.stringValue);
        if (selectedAudioLanguage != null) {
            selectedAudioLanguageIndex = selectedAudioLanguage.index;
        }
        selectedAudioLanguageIndex = EditorGUILayout.Popup("Audio Language", selectedAudioLanguageIndex, Cultures.AvailableCulturesDisplayNames);

        audioLanguageProp.stringValue = selectedAudioLanguageIndex != -1
            ? Cultures.AvailableCulturesNames[selectedAudioLanguageIndex]
            : System.Globalization.CultureInfo.CurrentCulture.Name;


        // Load options from disk
        if (GUILayout.Button("GetValuesFromDisk")) {
            (target as Settings).ReadSettingsFromDisk();
        }

        // Write options to disk
        if (GUILayout.Button("WriteValuesToDisk")) {
            (target as Settings).WriteSettingsToDisk();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
