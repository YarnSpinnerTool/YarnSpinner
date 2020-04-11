using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Yarn;

/// A custom editor that lists the nodes in the program.
[CustomEditor(typeof(YarnProgram))]
public class YarnProgramEditor : Editor {

    private List<string> nodeNames;

    public void OnEnable() {
        var program = (serializedObject.targetObject as YarnProgram).GetProgram();

        nodeNames = program.Nodes.Keys.ToList();        
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        if (nodeNames == null) {
            EditorGUILayout.HelpBox("Error reading Yarn program.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField($"{nodeNames.Count} nodes:", EditorStyles.boldLabel);
        EditorGUI.indentLevel += 1;

        foreach (var nodeName in nodeNames) {
            EditorGUILayout.LabelField(nodeName);
        }

        EditorGUI.indentLevel -= 1;
    }
}