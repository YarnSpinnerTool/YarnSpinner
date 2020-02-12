using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yarn.Unity {
    [CustomEditor(typeof(YarnLinesAsCanvasText))]
    public class YarnLinesAsCanvasTextEditor : Editor {
        private YarnProgram _yarnProgram = default;
        private SerializedProperty _yarnProgramProperty = default;
        private Dictionary<string, string> _yarnStringTable = new Dictionary<string, string>();
        private SerializedProperty _textObjectsProperty = default;
        private bool _showTextUiComponents = true;
        private const string _textUiComponentsLabel = "Text UI Components";
        private string _lastLanguageId = default;
        private GUIStyle _headerStyle;

        private void OnEnable() {
            _headerStyle = new GUIStyle() { fontStyle = FontStyle.Bold };
            _lastLanguageId = Preferences.TextLanguage;

            _yarnProgramProperty = serializedObject.FindProperty("yarnScript");
            if (_yarnProgramProperty.objectReferenceValue == null) {
                return;
            }

            _yarnProgram = _yarnProgramProperty.objectReferenceValue as YarnProgram;

            _yarnStringTable.Clear();
            foreach (var line in _yarnProgram.GetStringTable()) {
                _yarnStringTable.Add(line.Key, line.Value);
            }

            _textObjectsProperty = serializedObject.FindProperty("textCanvases");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_yarnProgramProperty);
            // Rebuild the string table if the yarn asset or the language preference has changed
            if (EditorGUI.EndChangeCheck() || _lastLanguageId != Preferences.TextLanguage) {
                OnEnable();
            }

            if (_yarnProgramProperty.objectReferenceValue == null) {
                EditorGUILayout.HelpBox("This component needs a yarn asset.", MessageType.Info);
            } else {
                _showTextUiComponents = EditorGUILayout.Foldout(_showTextUiComponents, _textUiComponentsLabel);
                if (_showTextUiComponents) {
                    if (_yarnStringTable.Count == 0) {
                        EditorGUILayout.HelpBox("Couldn't find any text lines on the referenced Yarn asset.", MessageType.Info);
                    } else {
                        // Header
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Yarn Text Lines", _headerStyle);
                        EditorGUILayout.LabelField("UI Canvas", _headerStyle);
                        EditorGUILayout.EndHorizontal();

                        // The referenced Canvas Text components
                        var i = 0;
                        foreach (var stringTableEntry in _yarnStringTable) {
                            if (_textObjectsProperty.arraySize <= i) {
                                _textObjectsProperty.InsertArrayElementAtIndex(i);
                            }
                            // Draw the actual content of the yarn line as lable so the user knows what text 
                            // will placed on the referenced component
                            GUIContent label = new GUIContent() { text = "'" + stringTableEntry.Value + "'" } ;
                            EditorGUILayout.PropertyField(_textObjectsProperty.GetArrayElementAtIndex(i), label);
                            i++;
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}