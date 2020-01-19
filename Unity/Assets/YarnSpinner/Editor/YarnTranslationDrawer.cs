using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(YarnTranslation))]
public class YarnTranslationDrawer : PropertyDrawer
{
    /// <summary>
    /// List of possible language display names for this translation.
    /// </summary>
    Culture[] cultureInfo = CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Where(c => c.Name != "")
                .Select(c => new Culture { Name = c.Name, DisplayName = c.DisplayName })
                .Append(new Culture { Name = "mi", DisplayName = "Maori" })
                .OrderBy(c => c.DisplayName)
                .ToArray();
    
    /// <summary>
    /// The index of the language display name associated with this translation
    /// </summary>
    private int selectedLanguageIndex;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		label = EditorGUI.BeginProperty(position, label, property);

        // The language ID
		Rect contentPosition = position;
		contentPosition.width *= 0.25f;
		EditorGUI.indentLevel = 1;
        var cultures = cultureInfo.Select(c => $"{c.Name}").ToArray();
        var culturesDisplayNames = cultureInfo.Select(c => $"{c.DisplayName}").ToArray();
        selectedLanguageIndex = EditorGUI.Popup(contentPosition, System.Array.IndexOf(cultures, property.FindPropertyRelative("languageName").stringValue), culturesDisplayNames);
        // Apply changed language ID
        if (selectedLanguageIndex != -1) {
            property.FindPropertyRelative("languageName").stringValue = cultures[selectedLanguageIndex];
        }

        // The yarn translation file
        contentPosition.x += contentPosition.width;
		contentPosition.width *= 3f;
		EditorGUI.PropertyField(contentPosition, property.FindPropertyRelative("text"), GUIContent.none);

		EditorGUI.EndProperty();
	}
}
