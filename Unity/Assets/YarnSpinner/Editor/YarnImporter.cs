
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.IO;

using System.Linq;

using Yarn;
using Yarn.Compiler;
using System.Globalization;
using System.Collections.Generic;

using CsvHelper;
using System;

[CustomEditor(typeof(YarnImporter))]
public class YarnImporterEditor : Editor {

    int selectedLanguageIndex;

    int selectedNewTranslationLanguageIndex;

    // We use a custom type to refer to cultures, because certain cultures
    // that we want to support don't exist in the .NET database (like Māori)

    struct Culture {
        public string Name;
        public string DisplayName;
    }

    Culture[] cultureInfo;
    
    SerializedProperty baseLanguageProp;

    private void OnEnable() {

        cultureInfo = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(c => c.Name != "")
            .Select(c => new Culture { Name = c.Name, DisplayName = c.DisplayName })
            .Append(new Culture { Name = "mi", DisplayName = "Maori" })
            .OrderBy(c => c.DisplayName)            
            .ToArray();

        baseLanguageProp = serializedObject.FindProperty("baseLanguageID");

        selectedLanguageIndex = cultureInfo.Select((culture, index) => new {culture, index})
            .FirstOrDefault(pair => pair.culture.Name == baseLanguageProp.stringValue)
            .index;
        selectedNewTranslationLanguageIndex = selectedLanguageIndex;
        

    }


    public override void OnInspectorGUI() {

        EditorGUILayout.Space();

        var cultures = cultureInfo.Select(c => $"{c.DisplayName}");
        
        using (var check = new EditorGUI.ChangeCheckScope()) {
            selectedLanguageIndex = EditorGUILayout.Popup("Base Language", selectedLanguageIndex, cultures.ToArray());

            if (check.changed) {
                baseLanguageProp.stringValue = cultureInfo[selectedLanguageIndex].Name;   
                serializedObject.ApplyModifiedProperties();                
                (target as YarnImporter).SaveAndReimport();
            }
        }

        YarnImporter yarnImporter = (target as YarnImporter);

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
        
    }

    private void AddLineTagsToFile(string assetPath)
    {
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

[ScriptedImporter(1, new[] {"yarn", "yarnc"})]
public class YarnImporter : ScriptedImporter
{    

    

    // culture identifiers like en-US
    public string baseLanguageID;

    public string[] stringIDs;

    public bool AnyImplicitStringIDs => compilationStatus == Status.SucceededUntaggedStrings;
    public bool StringsAvailable => stringIDs?.Length > 0;

    public Status compilationStatus;
    
    public bool isSuccesfullyCompiled = false;

    public string compilationErrorMessage = null;

    private void OnValidate() {
        if (baseLanguageID == null) {
            baseLanguageID = CultureInfo.CurrentCulture.Name;
        }
    }

    public override bool SupportsRemappedAssetType(System.Type type) {
        if (type.IsAssignableFrom(typeof(TextAsset))) {
            return true;
        }
        return false;
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {

        var extension = System.IO.Path.GetExtension(ctx.assetPath);

        // Clear the list of strings, in case this compilation fails
        stringIDs = new string[] {};

        isSuccesfullyCompiled = false;

        if (extension == ".yarn")
        {
            ImportYarn(ctx);
        }
        else if (extension == ".yarnc")
        {
            ImportCompiledYarn(ctx);
        }
    }

    private void ImportYarn(AssetImportContext ctx)
    {
        var sourceText = File.ReadAllText(ctx.assetPath);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);

        try
        {
            // Compile the source code into a compiled Yarn program (or
            // generate a parse error)
            compilationStatus = Compiler.CompileString(sourceText, fileName, out var compiledProgram, out var stringTable);

            // Create a container for storing the bytes
            var programContainer = ScriptableObject.CreateInstance<YarnProgram>();                

            using (var memoryStream = new MemoryStream())
            using (var outputStream = new Google.Protobuf.CodedOutputStream(memoryStream))
            {

                // Serialize the compiled program to memory
                compiledProgram.WriteTo(outputStream);
                outputStream.Flush();

                byte[] compiledBytes = memoryStream.ToArray();

                programContainer.compiledProgram = compiledBytes;

                // Add this container to the imported asset; it will be
                // what the user interacts with in Unity
                ctx.AddObjectToAsset("Program", programContainer);
                ctx.SetMainObject(programContainer);

                isSuccesfullyCompiled = true;
            }

            if (stringTable.Count > 0) {
                using (var memoryStream = new MemoryStream()) 
                using (var textWriter = new StreamWriter(memoryStream)) {
                    // Generate the localised .csv file
                    var csv = new CsvHelper.CsvWriter(textWriter);

                    var lines = stringTable.Select(x => new {
                        id = x.Key, 
                        text=x.Value.text,
                        file=x.Value.fileName,
                        node=x.Value.nodeName,
                        lineNumber=x.Value.lineNumber
                    });

                    csv.WriteRecords(lines);

                    textWriter.Flush();

                    memoryStream.Position = 0;

                    using (var reader = new StreamReader(memoryStream)) {
                        var textAsset = new TextAsset(reader.ReadToEnd());
                        textAsset.name = $"{fileName} ({baseLanguageID})";

                        ctx.AddObjectToAsset("Strings", textAsset);

                        programContainer.baseLocalisationStringTable = textAsset;
                    }

                    stringIDs = lines.Select(l => l.id).ToArray();

                    
                }
            }

            

        }
        catch (Yarn.Compiler.ParseException e)
        {
            isSuccesfullyCompiled = false;
            compilationErrorMessage = e.Message;
            ctx.LogImportError(e.Message);
            return;
        }
    }

    private void ImportCompiledYarn(AssetImportContext ctx) {

        var bytes = File.ReadAllBytes(ctx.assetPath);

        try {
            // Validate that this can be parsed as a Program protobuf
            var _ = Program.Parser.ParseFrom(bytes);
        } catch (Google.Protobuf.InvalidProtocolBufferException) {
            ctx.LogImportError("Invalid compiled yarn file. Please re-compile the source code.");
            return;
        }

        isSuccesfullyCompiled = true;        

        // Create a container for storing the bytes
        var programContainer = ScriptableObject.CreateInstance<YarnProgram>();
        programContainer.compiledProgram = bytes;

        // Add this container to the imported asset; it will be
        // what the user interacts with in Unity
        ctx.AddObjectToAsset("Program", programContainer);
        ctx.SetMainObject(programContainer);
    }
}
