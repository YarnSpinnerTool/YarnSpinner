using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Yarn;

using System.IO;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(YarnProgram))]
public class YarnProgramEditor : Editor {

    private List<string> nodeNames = new List<string>();

    void OnEnable() {
        var program = (serializedObject.targetObject as YarnProgram).GetProgram();

        nodeNames = program.Nodes.Keys.ToList();
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        
        EditorGUILayout.LabelField($"{nodeNames.Count} nodes:", EditorStyles.boldLabel);
        EditorGUI.indentLevel += 1;

        foreach (var nodeName in nodeNames) {
            EditorGUILayout.LabelField(nodeName);
        }

        EditorGUI.indentLevel -= 1;
        
    }
}
#endif

public class YarnProgram : ScriptableObject
{
    [SerializeField]
    [HideInInspector]
    public byte[] compiledProgram;

    public Program GetProgram() {
        return Program.Parser.ParseFrom(compiledProgram);                
    } 
}
