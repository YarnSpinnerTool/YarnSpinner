
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using System.IO;

using Yarn;
using Yarn.Compiler;

[ScriptedImporter(1, new[] {"yarn", "yarnc"})]
public class YarnImporter : ScriptedImporter
{    
    public override void OnImportAsset(AssetImportContext ctx)
    {

        var extension = System.IO.Path.GetExtension(ctx.assetPath);

        if (extension == ".yarn")
        {
            ImportYarn(ctx);
        }
        else if (extension == ".yarnc")
        {
            ImportCompiledYarn(ctx);
        }
    }

    private static void ImportYarn(AssetImportContext ctx)
    {
        var sourceText = File.ReadAllText(ctx.assetPath);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);

        try
        {
            // Compile the source code into a compiled Yarn program (or
            // generate a parse error)
            var compiledProgram = Compiler.CompileString(sourceText, fileName);

            using (var memoryStream = new MemoryStream())
            using (var outputStream = new Google.Protobuf.CodedOutputStream(memoryStream))
            {

                // Serialize the compiled program to memory
                compiledProgram.WriteTo(outputStream);
                outputStream.Flush();

                byte[] compiledBytes = memoryStream.ToArray();

                // Create a container for storing the bytes
                var programContainer = ScriptableObject.CreateInstance<YarnProgram>();
                programContainer.compiledProgram = compiledBytes;

                // Add this container to the imported asset; it will be
                // what the user interacts with in Unity
                ctx.AddObjectToAsset("Program", programContainer);
                ctx.SetMainObject(programContainer);
            }

        }
        catch (Yarn.Compiler.ParseException e)
        {
            ctx.LogImportError(e.Message);
            return;
        }
    }

    private static void ImportCompiledYarn(AssetImportContext ctx) {

        var bytes = File.ReadAllBytes(ctx.assetPath);

        try {
            // Validate that this can be parsed as a Program protobuf
            var _ = Program.Parser.ParseFrom(bytes);
        } catch (Google.Protobuf.InvalidProtocolBufferException) {
            ctx.LogImportError("Invalid compiled yarn file. Please re-compile the source code.");
            return;
        }

        // Create a container for storing the bytes
        var programContainer = ScriptableObject.CreateInstance<YarnProgram>();
        programContainer.compiledProgram = bytes;

        // Add this container to the imported asset; it will be
        // what the user interacts with in Unity
        ctx.AddObjectToAsset("Program", programContainer);
        ctx.SetMainObject(programContainer);
    }
}
