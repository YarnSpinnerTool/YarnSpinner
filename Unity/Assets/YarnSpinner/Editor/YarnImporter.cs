using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using System.IO;
using System.Linq;
using System.Globalization;
using Yarn;
using Yarn.Compiler;

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

    public TextAsset baseLanguage;
    public YarnTranslation[] localizations = new YarnTranslation[0];

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
                        baseLanguage = textAsset;
                        programContainer.localizations = localizations;
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
