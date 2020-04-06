using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Collections.Generic;

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

        if (string.IsNullOrEmpty(baseLanguageProp.stringValue)) {
            selectedLanguageIndex = cultureInfo.
                Select((culture, index) => new { culture, index })
                .FirstOrDefault(element => element.culture.Name == CultureInfo.CurrentCulture.Name)
                .index;
        } else {
            selectedLanguageIndex = cultureInfo.Select((culture, index) => new { culture, index })
                .FirstOrDefault(pair => pair.culture.Name == baseLanguageProp.stringValue)
                .index;
        }
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
        // Array of translations that have been added to this asset + base language
        var culturesAvailableOnAsset = yarnImporter.localizations.
            Select(element => element.languageName).
            Append(cultureInfo[selectedLanguageIndex].Name).
            OrderBy(element => element).
            ToArray();

        selectedLanguageIndex = EditorGUILayout.Popup("Base Language", selectedLanguageIndex, cultures.ToArray());
        baseLanguageProp.stringValue = cultureInfo[selectedLanguageIndex].Name;

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

                // Automatically add newly created translation csv file to yarn program
                var localizationsIndex = System.Array.FindIndex(yarnImporter.localizations, element => element.languageName == selectedCulture.Name);
                var localizationSerializedProperty = serializedObject.FindProperty("localizations");
                if (localizationsIndex != -1) {
                    localizationSerializedProperty.GetArrayElementAtIndex(localizationsIndex).FindPropertyRelative("text").objectReferenceValue = asset;
                } else {
                    localizationSerializedProperty.InsertArrayElementAtIndex(localizationSerializedProperty.arraySize);
                    localizationSerializedProperty.GetArrayElementAtIndex(localizationSerializedProperty.arraySize-1).FindPropertyRelative("text").objectReferenceValue = asset;
                    localizationSerializedProperty.GetArrayElementAtIndex(localizationSerializedProperty.arraySize-1).FindPropertyRelative("languageName").stringValue = selectedCulture.Name;
                }
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
        else if (serializedObject.FindProperty("localizations").arraySize>0)
        {
            if (GUILayout.Button("Update Localizations"))
            {
                UpdateLocalizations(AssetDatabase
                        .LoadAllAssetsAtPath(yarnImporter.assetPath)
                        .OfType<TextAsset>()
                        .FirstOrDefault()?
                        .text ?? "");
            }
        }

        // Localization list
        EditorGUILayout.PropertyField(serializedObject.FindProperty("localizations"), true);

        var success = serializedObject.ApplyModifiedProperties();
#if UNITY_2018
        if (success) {
            EditorUtility.SetDirty(target);
            AssetDatabase.WriteImportSettingsIfDirty(AssetDatabase.GetAssetPath(target));
        }
#endif
#if UNITY_2019_1_OR_NEWER
        ApplyRevertGUI();
#endif
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

    void UpdateLocalizations(string newBaseCsv)
    {
        YarnImporter yarnImporter = (target as YarnImporter);

        SerializedProperty sp = serializedObject.FindProperty("localizations");
        for (int i = 0; i < sp.arraySize; i++)
        {
            SerializedProperty spe = sp.GetArrayElementAtIndex(i);
            SerializedProperty spt = spe.FindPropertyRelative("text");
            TextAsset ta = (TextAsset)spt.objectReferenceValue;
            string s = ta.text;
            string merged = MergeStringTables(s, newBaseCsv, Path.GetFileNameWithoutExtension(yarnImporter.assetPath));

            // Save to file
            var assetDirectory = Path.GetDirectoryName(yarnImporter.assetPath);
            string language = spe.FindPropertyRelative("languageName").stringValue;

            var newStringsTablePath = $"{assetDirectory}/{Path.GetFileNameWithoutExtension(yarnImporter.assetPath)} ({language}).csv";
            newStringsTablePath = AssetDatabase.GenerateUniqueAssetPath(newStringsTablePath);

            var writer = File.CreateText(newStringsTablePath);
            writer.Write(merged);
            writer.Close();

            AssetDatabase.ImportAsset(newStringsTablePath);

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(newStringsTablePath);

            EditorGUIUtility.PingObject(asset);
        }
    }

    string MergeStringTables(string oldLocalizedCsv, string newBaseCsv, string outputFileName)
    {
        List<CsvEntry> oldEntries = new List<CsvEntry>();
        List<CsvEntry> newEntries = new List<CsvEntry>();

        using (var oldReader = new StringReader(oldLocalizedCsv))
        using (var newReader = new StringReader(newBaseCsv))
        {
            CsvHelper.CsvReader oldParser = new CsvHelper.CsvReader(oldReader, new CsvHelper.Configuration.Configuration(CultureInfo.InvariantCulture));
            CsvHelper.CsvReader newParser = new CsvHelper.CsvReader(newReader, new CsvHelper.Configuration.Configuration(CultureInfo.InvariantCulture));

            oldParser.Read();
            newParser.Read();

            oldParser.ReadHeader();
            newParser.ReadHeader();

            while (oldParser.Read())
            {
                oldEntries.Add(
                    new CsvEntry
                    {
                        id = oldParser.GetField("id"),
                        text = oldParser.GetField("text"),
                        file = oldParser.GetField("file"),
                        node = oldParser.GetField("node"),
                        lineNumber = oldParser.GetField<int>("lineNumber"),
                    }
                );
            }

            while (newParser.Read())
            {
                newEntries.Add(
                    new CsvEntry
                    {
                        id = newParser.GetField("id"),
                        text = newParser.GetField("text"),
                        file = newParser.GetField("file"),
                        node = newParser.GetField("node"),
                        lineNumber = newParser.GetField<int>("lineNumber"),
                    }
                );
            }

        }


        // Here's what's happening:
        // Use CsvParser to parse new base string table and old localized string table
        // The strategy is to use the fact that the two string tables look alike to optimize.
        // The algorithm goes through the string tables side by side. Imagine two fingers running
        // through the entries. At each line there are four different scenarios that we test for:
        // scenario 1: The lines match (matching tags)
        // scenario 2: The line in the new string table exists in the old string table but has been moved from somewhere else
        // scenario 3: The line in the new string table is completely new
        // scenario 4: The line in the old string table has been deleted

        //Go line by line:
        //1.If line tags are the same: add old localized with new line number and node. Increase index of both. (s1: matching lines)
        //2.Else if line tags are different:
        //  a. Search forward in the old string table for that line tag
        //    i. if we find it: add old localized with new line number and node. Remove from old. Increase new index. (s2: old line moved)
        //    ii. If we don't find it: Search forward in new string table for that line tag
        //      I. if we find it: add the one new line we are on. Increase new index. (s3: line is new)
        //      II. if we don't find it: ignore line. Increase old index. (s4: line has been deleted)
        int oldIndex = 0;
        int newIndex = 0;

        List<CsvEntry> mergedEntries = new List<CsvEntry>();

        while (true)
        {
            // If no more entries in old: add all last new entries and break
            if (oldEntries.Count <= oldIndex)
            {
                for (int i = newIndex; i < newEntries.Count; i++)
                {
                    mergedEntries.Add(newEntries[i]);
                }
                break;
            }

            // If no more entries in new: break
            if (newEntries.Count <= newIndex)
            {
                break;
            }

            //1. If line tags are the same: add old localized with new line number. Increase index of both.
            if (oldEntries[oldIndex].id == newEntries[newIndex].id)
            {
                CsvEntry entry = oldEntries[oldIndex];
                entry.lineNumber = newEntries[newIndex].lineNumber;
                entry.node = newEntries[newIndex].node;
                mergedEntries.Add(entry);
                oldIndex++;
                newIndex++;
                continue;
            }
            //2. Else if line tags are different:
            else
            {
                // a. Search forward in the old string table for that line tag
                bool didFindInOld = false;
                for (int i = oldIndex + 1; i < oldEntries.Count; i++)
                {
                    // i. if we find it: add old localized with new line number. Remove from old. Increase index of new. (old line moved)
                    if (oldEntries[i].id == newEntries[newIndex].id)
                    {
                        CsvEntry entry = oldEntries[i];
                        entry.lineNumber = newEntries[newIndex].lineNumber;
                        entry.node = newEntries[newIndex].node;
                        mergedEntries.Add(entry);
                        oldEntries.RemoveAt(i);
                        didFindInOld = true;
                        newIndex++;
                        break;
                    }
                }
                if (didFindInOld)
                {
                    continue;
                }

                // ii.If we don't find it: Search forward in new string table for that line tag
                bool didFindInNew = false;
                for (int i = newIndex + 1; i < newEntries.Count; i++)
                {
                    // I. if we find it: add the one new line we are on. Increase index of new. (line is new)
                    if (oldEntries[oldIndex].id == newEntries[i].id)
                    {
                        CsvEntry entry = newEntries[newIndex];
                        entry.text += " (((NEW LINE)))";
                        mergedEntries.Add(entry);
                        newIndex++;
                        didFindInNew = true;
                        break;
                    }
                }
                // II. if we don't find it: ignore line. Increase index of old. (line has been deleted)
                if (!didFindInNew)
                {
                    oldIndex++;
                }
            }
        }

        mergedEntries.Sort((a, b) => a.lineNumber.CompareTo(b.lineNumber));

        //foreach (CsvEntry e in mergedEntries)
        //{
        //    Debug.Log(e.text + " : " + e.lineNumber);
        //}

        // Create new Csv file
        using (var memoryStream = new MemoryStream())
        using (var textWriter = new StreamWriter(memoryStream))
        {
            // Generate the localised .csv file
            var csv = new CsvHelper.CsvWriter(textWriter, new CsvHelper.Configuration.Configuration(CultureInfo.InvariantCulture));

            var lines = mergedEntries.Select(x => new
            {
                id = x.id,
                text = x.text,
                file = outputFileName,
                node = x.node,
                lineNumber = x.lineNumber
            });

            csv.WriteRecords(lines);

            textWriter.Flush();

            memoryStream.Position = 0;

            using (var reader = new StreamReader(memoryStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
    
    public struct CsvEntry
    {
        public string id;
        public string text;
        public string file;
        public string node;
        public int lineNumber;
    }
}