using CsvHelper;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuYarnUI : MonoBehaviour {
    public YarnProgram yarnScript;
    private Dictionary<string, string> strings = new Dictionary<string, string>();
    public UnityEngine.UI.Text[] textObjects;

    private void Awake() {
        var textToLoad = new TextAsset();
        if (yarnScript.localizations != null || yarnScript.localizations.Length > 0) {
            textToLoad = Array.Find(yarnScript.localizations, element => element.languageName == Preferences.TextLanguage)?.text;
        }
        if (textToLoad == null || string.IsNullOrEmpty(textToLoad.text)) {
            textToLoad = yarnScript.baseLocalisationStringTable;
        }

        using (var reader = new System.IO.StringReader(textToLoad.text))
        using (var csv = new CsvReader(reader)) {
            csv.Read();
            csv.ReadHeader();

            while (csv.Read()) {
                strings.Add(csv.GetField("id"), csv.GetField("text"));
            }
        }
    }

    // Start is called before the first frame update
    void Start() {
        var index = 0;
        foreach (var line in strings) {
            if (index >= textObjects.Length) {
                return;
            }

            textObjects[index].text = line.Value;
            index++;
        }
    }
}
