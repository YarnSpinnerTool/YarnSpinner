using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(Culture))]
public class CultureDrawer : PropertyDrawer {

    private int selectedLanguageIndex;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);

        // Show the Label of this language (text language? audio language?)
        Rect contentPosition = position;
        contentPosition.width = 110;
        EditorGUI.LabelField(contentPosition, label);

        // Show a dropdown menu of available languages
        var selectedLanguageNameProp = property.FindPropertyRelative("Name");
        var selectedLanguageDisplayNameProp = property.FindPropertyRelative("DisplayName");
        contentPosition.x += contentPosition.width;
        contentPosition.width = position.width - contentPosition.width > 300 ? 300 : position.width - contentPosition.width;
        selectedLanguageIndex = EditorGUI.Popup(contentPosition, System.Array.IndexOf(
            Cultures.AvailableCulturesNames, 
            property.FindPropertyRelative("Name").stringValue), 
            Cultures.AvailableCulturesDisplayNames);
        
        // Apply changed values immediately to RAM. Unless this is not part of an Editor script applying the modified values, 
        // this Will be written to disk upon pressing CTRL+S or when closing Unity.
        if (selectedLanguageIndex != -1) {
            selectedLanguageNameProp.stringValue = Cultures.AvailableCulturesNames[selectedLanguageIndex];
            selectedLanguageDisplayNameProp.stringValue = Cultures.AvailableCulturesDisplayNames[selectedLanguageIndex];
        }

		EditorGUI.EndProperty();
    }

}
