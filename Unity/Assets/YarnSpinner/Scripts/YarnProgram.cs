using UnityEngine;
using Yarn;

/// Stores compiled Yarn programs in a form that Unity can serialise.
public class YarnProgram : ScriptableObject
{
    [SerializeField]
    [HideInInspector]
    public byte[] compiledProgram;

    [SerializeField]
    public TextAsset baseLocalisationStringTable;

    [SerializeField]
    public string baseLocalizationId;

    /// <summary>
    /// Available localizations of this yarn program
    /// </summary>
    [SerializeField][HideInInspector]
    public YarnTranslation[] localizations = new YarnTranslation[0];

    // Deserializes a compiled Yarn program from the stored bytes in this
    // object.
    public Program GetProgram() {
        return Program.Parser.ParseFrom(compiledProgram);                
    } 
}
