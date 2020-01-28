using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ProjectSettings : ScriptableObject {
    public List<string> _projectLanguages = new List<string>();

    public List<string> _textProjectLanguages = new List<string>();

    public List<string> _audioProjectLanguages = new List<string>();

    /// <summary>
    /// The path to store the project settings
    /// </summary>
    private string _preferencesPath;

    /// <summary>
    /// Instance of this class (Singleton design pattern)
    /// </summary>
    private static ProjectSettings _instance;

    /// <summary>
    /// Makes sure that there's always an instance of this 
    /// class alive upon access.
    /// </summary>
    public static ProjectSettings Instance {
        get {
            if (!_instance) {
                _instance = CreateInstance<ProjectSettings>();
            }
            return _instance;
        }
    }

    private void Awake() {
        if (_instance != null && this != _instance) {
            DestroyImmediate(_instance);
        }
        _instance = this;

#if UNITY_EDITOR
        _preferencesPath = Application.dataPath + "/../ProjectSettings" + "/YarnProjectSettings.json";
#endif
#if UNITY_PLAYER
        _preferencesPath = "YarnProjectSettings.json"
#endif

        YarnSettingsHelper.ReadPreferencesFromDisk(this, _preferencesPath);
    }

    private void OnDestroy() {
        YarnSettingsHelper.WritePreferencesToDisk(this, _preferencesPath);
    }
}
