using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yarn.Unity {
    [CustomEditor(typeof(YarnLinesAsCanvasText))]
    public class YarnLinesAsCanvasTextEditor : Editor {
        private YarnProgram _yarnProgram = default;
        private SerializedProperty _yarnProgramProperty = default;
        private Dictionary<string, string> _yarnLines = new Dictionary<string, string>();
        private SerializedProperty _textObjectsProperty = default;
        private bool ShowUIElements = true;
        private const string textObjects = "Text Objects";
        private string lastUpdateLanguage = default;

        private void OnEnable() {
            _yarnProgramProperty = serializedObject.FindProperty("yarnScript");
            if (_yarnProgramProperty.objectReferenceValue == null) {
                return;
            }

            _yarnProgram = _yarnProgramProperty.objectReferenceValue as YarnProgram;

            _yarnLines.Clear();
            foreach (var line in _yarnProgram.GetStringTable()) {
                _yarnLines.Add(line.Key, line.Value);
            }

            _textObjectsProperty = serializedObject.FindProperty("textObjects");

            lastUpdateLanguage = Preferences.TextLanguage;
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_yarnProgramProperty);
            if (EditorGUI.EndChangeCheck() || lastUpdateLanguage != Preferences.TextLanguage) {
                OnEnable();
            }

            if (_yarnProgramProperty.objectReferenceValue == null) {
                EditorGUILayout.HelpBox("This component needs a yarn asset.", MessageType.Info);
            } else {
                var InventoryHeaderStyle = new GUIStyle();
                InventoryHeaderStyle.fontStyle = FontStyle.Bold;

                ShowUIElements = EditorGUILayout.Foldout(ShowUIElements, textObjects);
                if (ShowUIElements) {
                    if (_yarnLines.Count == 0) {
                        EditorGUILayout.HelpBox("Couldn't find any text lines on the referenced Yarn asset.", MessageType.Info);
                    } else {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Yarn Text Lines", InventoryHeaderStyle);
                        EditorGUILayout.LabelField("UI Canvas", InventoryHeaderStyle);
                        EditorGUILayout.EndHorizontal();
                        var i = 0;
                        foreach (var stringEntry in _yarnLines) {
                            if (_textObjectsProperty.arraySize <= i) {
                                _textObjectsProperty.InsertArrayElementAtIndex(i);
                            }
                            GUIContent label = new GUIContent();
                            label.text = "'" + stringEntry.Value + "'";
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