using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

/// Stores compiled Yarn programs in a form that Unity can serialise.
public class YarnProgram : ScriptableObject
{
    [SerializeField]
    [HideInInspector]
    public byte[] compiledProgram;

    [SerializeField]
    public TextAsset baseLocalisationStringTable;

    // Deserializes a compiled Yarn program from the stored bytes in this
    // object.
    public Program GetProgram() {
        return Program.Parser.ParseFrom(compiledProgram);                
    } 
}

#if UNITY_EDITOR
/// A custom editor that lists the nodes in the program.
[CustomEditor(typeof(YarnProgram))]
public class YarnProgramEditor : Editor {

    private List<string> nodeNames;

    void OnEnable() {
        try {
            var program = (serializedObject.targetObject as YarnProgram).GetProgram();

            nodeNames = program.Nodes.Keys.ToList();
        } catch (YarnException) {
            nodeNames = null;
        }
        
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
#endif


