using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(YarnTranslation))]
public class YarnTranslationDrawer : PropertyDrawer 
    {
    /// <summary>
    /// The index of the language display name associated with this translation
    /// </summary>
    private int selectedLanguageIndex;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);

        var availableCulturesNames = Cultures.AvailableCulturesNames;

        if (ProjectSettingsProvider.ProjectSettingsSerialized != null) {
            var projectSettingsTextLanguages = (ProjectSettingsProvider.ProjectSettingsSerialized.targetObject as ProjectSettings)._textProjectLanguages;
            if (projectSettingsTextLanguages.Count > 0 && projectSettingsTextLanguages.Contains(property.FindPropertyRelative("languageName").stringValue)) {
                // Only reduce text languages to selection from project settings if the current language is available in that list
                availableCulturesNames = projectSettingsTextLanguages.ToArray();
            }
        }

        // The language ID
        Rect contentPosition = position;
        contentPosition.width *= 0.25f;
        EditorGUI.indentLevel = 1;
        selectedLanguageIndex = EditorGUI.Popup(contentPosition, System.Array.IndexOf(availableCulturesNames, property.FindPropertyRelative("languageName").stringValue), Cultures.LanguageNamesToDisplayNames(availableCulturesNames));
        // Apply changed language ID
        if (selectedLanguageIndex != -1) {
            property.FindPropertyRelative("languageName").stringValue = availableCulturesNames[selectedLanguageIndex];
        }

        // The yarn translation file
        contentPosition.x += contentPosition.width;
        contentPosition.width *= 3f;
        EditorGUI.PropertyField(contentPosition, property.FindPropertyRelative("text"), GUIContent.none);

        EditorGUI.EndProperty();
    }
}
