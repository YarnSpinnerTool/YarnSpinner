﻿using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.IO;
using System.Linq;
using System.Globalization;
using Yarn;
using Yarn.Compiler;
using Boo.Lang;

[ScriptedImporter(2, new[] {"yarn", "yarnc"})]
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

    public TextAsset baseLanguage;
    public YarnTranslation[] localizations = new YarnTranslation[0];
    public LinetagToLanguage[] voiceOvers = new LinetagToLanguage[0];
    public YarnProgram programContainer = default;

    private void OnValidate() {
        if (string.IsNullOrEmpty(baseLanguageID)) {
            // If the user has added project wide text languages in the settings 
            // dialogue, we default to the first text language as base language
            if (ProjectSettings.TextProjectLanguages.Count > 0) {
                baseLanguageID = ProjectSettings.TextProjectLanguages[0];
            // Otherwrise use system's language as base language
            } else {
                baseLanguageID = CultureInfo.CurrentCulture.Name;
            }
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
        OnValidate();
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

#if ADDRESSABLES
    /// <summary>
    /// Remove all voice over audio clip references or addressable references on this yarn asset.
    /// </summary>
    /// <param name="removeDirectReferences">True if direct audio clip references should be deleted and false if Addressable references should be deleted.</param>
    public void RemoveAllVoiceOverReferences(bool removeDirectReferences) {
        foreach (var linetag in voiceOvers) {
            foreach (var language in linetag.languageToAudioclip) {
                if (removeDirectReferences) {
                    language.audioClip = null;
                } else {
                    language.audioClipAddressable = null;
                }
            }
        }
    }
#endif

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
            programContainer = ScriptableObject.CreateInstance<YarnProgram>();

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
                ctx.AddObjectToAsset("Program", programContainer, AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath("528a6dd601766934abb8b1053bd798ef"), typeof(Texture2D)) as Texture2D);
                ctx.SetMainObject(programContainer);

                isSuccesfullyCompiled = true;

                // var outPath = Path.ChangeExtension(ctx.assetPath, ".yarnc");
                // File.WriteAllBytes(outPath, compiledBytes);
            }

            if (stringTable.Count > 0) {

                

                using (var memoryStream = new MemoryStream()) 
                using (var textWriter = new StreamWriter(memoryStream)) {
                    // Generate the localised .csv file

                    // Use the invariant culture when writing the CSV
                    var configuration = new CsvHelper.Configuration.Configuration(
                        System.Globalization.CultureInfo.InvariantCulture
                    );

                    var csv = new CsvHelper.CsvWriter(
                        textWriter, // write into this stream
                        configuration // use this configuration
                        );

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
                        programContainer.baseLocalizationId = baseLanguageID;
                        baseLanguage = textAsset;
                        programContainer.localizations = localizations;
                        programContainer.baseLocalizationId = baseLanguageID;
                    }

                    stringIDs = lines.Select(l => l.id).ToArray();

                    var voiceOversList = voiceOvers.ToList();
                    // Init voice overs by writing all linetags of this yarn program for every available translation
                    foreach (var textEntry in stringIDs) {
                        if (voiceOversList.Find(element => element.linetag == textEntry) == null) {
                            voiceOversList.Add(new LinetagToLanguage(textEntry));
                        }

                        var languageToAudioclipList = voiceOversList.Find(element => element.linetag == textEntry).languageToAudioclip.ToList();
                        foreach (var localization in localizations) {
                            if (!ProjectSettings.AudioProjectLanguages.Contains(localization.languageName)) {
                                continue;
                            }

                            if (languageToAudioclipList.Find(element => element.language == localization.languageName) == null) {
                                languageToAudioclipList.Add(new LanguageToAudioclip(localization.languageName));
                            }
                        }

                        // Also initialize for base language ID
                        if (!string.IsNullOrEmpty(baseLanguageID) && languageToAudioclipList.Find(element => element.language == baseLanguageID) == null) {
                            languageToAudioclipList.Add(new LanguageToAudioclip(baseLanguageID));
                        }

                        // Remove empty entries; shouldn't be necessary though
                        if (languageToAudioclipList.Find(element => string.IsNullOrEmpty(element.language)) != null) {
                            languageToAudioclipList.Remove(languageToAudioclipList.Find(element => string.IsNullOrEmpty(element.language)));
                        }

                        voiceOversList.Find(element => element.linetag == textEntry).languageToAudioclip = languageToAudioclipList.ToArray();
                    }

                    // Check if previously stored linetags have been removed and remove them from the voice over collection
                    for (int i = voiceOversList.Count - 1; i >= 0; i--)
                    {
                        LinetagToLanguage voiceOver = voiceOversList[i];
                        if (!stringIDs.Contains(voiceOver.linetag))
                        {
                            voiceOversList.Remove(voiceOver);
                        }
                    }

                    voiceOvers = voiceOversList.ToArray();
                    programContainer.voiceOvers = voiceOvers;
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
