using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ProjectSettings : ScriptableObject {
    public List<string> _projectLanguages = new List<string>();

    public List<string> _textProjectLanguages = new List<string>();

    public List<string> _audioProjectLanguages = new List<string>();
}
