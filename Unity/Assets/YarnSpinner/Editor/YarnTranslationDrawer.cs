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
        SerializedProperty languageNameProperty = property.FindPropertyRelative("languageName");

        // Only reduce text languages to selection from project settings if the current language is available in that list
        if (ProjectSettings.TextProjectLanguages.Count > 0 && (ProjectSettings.TextProjectLanguages.Contains(languageNameProperty.stringValue) || string.IsNullOrEmpty(languageNameProperty.stringValue))) {
            availableCulturesNames = ProjectSettings.TextProjectLanguages.ToArray();
            // Check if this drawer is used on an asset utilizing a baseLanguageID and remove that from the available cultures to avoid double entries
            var baseLanguageIdProperty = property.serializedObject.FindProperty("baseLanguageID").stringValue;
            if (!string.IsNullOrEmpty(baseLanguageIdProperty)) {
                availableCulturesNames = availableCulturesNames.Except(new string[] { baseLanguageIdProperty } ).ToArray();
            }
            // Assign the first localization language if no language has been selected (user has not clicked on "Create New Localisation" but expanded the localization list via the "Size" property)
            if (string.IsNullOrEmpty(languageNameProperty.stringValue) && availableCulturesNames.Length > 0)
            {
                languageNameProperty.stringValue = availableCulturesNames[0];
            }
        }

        // The language ID
        Rect contentPosition = position;
        contentPosition.width *= 0.25f;
        EditorGUI.indentLevel = 1;
        selectedLanguageIndex = EditorGUI.Popup(contentPosition, System.Array.IndexOf(availableCulturesNames, languageNameProperty.stringValue), Cultures.LanguageNamesToDisplayNames(availableCulturesNames));
        // Apply changed language ID
        if (selectedLanguageIndex != -1) {
            languageNameProperty.stringValue = availableCulturesNames[selectedLanguageIndex];
        }

        // The yarn translation file
        contentPosition.x += contentPosition.width;
        contentPosition.width *= 3f;
        EditorGUI.PropertyField(contentPosition, property.FindPropertyRelative("text"), GUIContent.none);

        EditorGUI.EndProperty();
    }
}
