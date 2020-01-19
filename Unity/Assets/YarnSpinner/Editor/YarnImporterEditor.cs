using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.Globalization;
using System.Linq;
using System.IO;

[CustomEditor(typeof(YarnImporter))]
public class YarnImporterEditor : ScriptedImporterEditor {

    int selectedLanguageIndex;

    int selectedNewTranslationLanguageIndex;


    // We use a custom type to refer to cultures, because certain cultures
    // that we want to support don't exist in the .NET database (like Māori)
    Culture[] cultureInfo;

    SerializedProperty baseLanguageProp;

    public override void OnEnable() {
        base.OnEnable();
            cultureInfo = CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Where(c => c.Name != "")
                .Select(c => new Culture { Name = c.Name, DisplayName = c.DisplayName })
                .Append(new Culture { Name = "mi", DisplayName = "Maori" })
                .OrderBy(c => c.DisplayName)
                .ToArray();

            baseLanguageProp = serializedObject.FindProperty("baseLanguageID");

            selectedLanguageIndex = cultureInfo.Select((culture, index) => new { culture, index })
                .FirstOrDefault(pair => pair.culture.Name == baseLanguageProp.stringValue)
                .index;
            selectedNewTranslationLanguageIndex = selectedLanguageIndex;
    }

    public override void OnDisable() {
        base.OnDisable();
    }


    public override void OnInspectorGUI() {
        serializedObject.Update();
        EditorGUILayout.Space();
        YarnImporter yarnImporter = (target as YarnImporter);

        var cultures = cultureInfo.Select(c => $"{c.DisplayName}");

        selectedLanguageIndex = EditorGUILayout.Popup("Base Language", selectedLanguageIndex, cultures.ToArray());
        baseLanguageProp.stringValue = cultureInfo[selectedLanguageIndex].Name;
        serializedObject.ApplyModifiedProperties();

        if (yarnImporter.isSuccesfullyCompiled == false) {
            EditorGUILayout.HelpBox(yarnImporter.compilationErrorMessage, MessageType.Error);
            return;
        }

        EditorGUILayout.Space();

        var canCreateLocalisation = yarnImporter.StringsAvailable == true && yarnImporter.AnyImplicitStringIDs == false;

        using (new EditorGUI.DisabledScope(!canCreateLocalisation))
        using (new EditorGUILayout.HorizontalScope()) {

            selectedNewTranslationLanguageIndex = EditorGUILayout.Popup(selectedNewTranslationLanguageIndex, cultures.ToArray());

            if (GUILayout.Button("Create New Localisation", EditorStyles.miniButton)) {
                var stringsTableText = AssetDatabase
                    .LoadAllAssetsAtPath(yarnImporter.assetPath)
                    .OfType<TextAsset>()
                    .FirstOrDefault()?
                    .text ?? "";

                var selectedCulture = cultureInfo[selectedNewTranslationLanguageIndex];

                var assetDirectory = Path.GetDirectoryName(yarnImporter.assetPath);

                var newStringsTablePath = $"{assetDirectory}/{Path.GetFileNameWithoutExtension(yarnImporter.assetPath)} ({selectedCulture.Name}).csv";
                newStringsTablePath = AssetDatabase.GenerateUniqueAssetPath(newStringsTablePath);

                var writer = File.CreateText(newStringsTablePath);
                writer.Write(stringsTableText);
                writer.Close();

                AssetDatabase.ImportAsset(newStringsTablePath);

                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(newStringsTablePath);

                EditorGUIUtility.PingObject(asset);
                serializedObject.ApplyModifiedProperties();
            }
        }

        if (yarnImporter.StringsAvailable == false) {
            EditorGUILayout.HelpBox("This file doesn't contain any localisable lines or options.", MessageType.Info);
        }

        if (yarnImporter.AnyImplicitStringIDs) {
            EditorGUILayout.HelpBox("Add #line: tags to all lines and options to enable creating new localisations. Either add them manually, or click Add Line Tags to automatically add tags. Note that this will modify your files on disk, and cannot be undone.", MessageType.Info);
            if (GUILayout.Button("Add Line Tags")) {
                AddLineTagsToFile(yarnImporter.assetPath);
            }
        }
        serializedObject.ApplyModifiedProperties();
        ApplyRevertGUI();
    }

    private void AddLineTagsToFile(string assetPath) {
        // First, gather all existing line tags, so that we don't
        // accidentally overwrite an existing one. Do this by finding _all_
        // YarnPrograms, and by extension their importers, and get the
        // string tags that they found.

        var allLineTags = Resources.FindObjectsOfTypeAll<YarnProgram>() // get all yarn programs that have been imported
            .Select(asset => AssetDatabase.GetAssetOrScenePath(asset)) // get the path on disk
            .Select(path => AssetImporter.GetAtPath(path)) // get the asset importer for that path
            .OfType<YarnImporter>() // ensure that they're all YarnImporters
            .SelectMany(importer => importer.stringIDs)
            .ToList(); // get all string IDs, flattened into one list            

        var contents = File.ReadAllText(assetPath);
        var taggedVersion = Yarn.Compiler.Utility.AddTagsToLines(contents, allLineTags);

        File.WriteAllText(assetPath, taggedVersion);

        AssetDatabase.ImportAsset(assetPath);
    }
}