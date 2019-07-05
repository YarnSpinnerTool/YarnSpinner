using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Yarn
{
    public static class ProgramExporter
    {

        internal static int Export(CompileOptions options)
        {
            YarnSpinnerConsole.CheckFileList(options.files, YarnSpinnerConsole.ALLOWED_EXTENSIONS);

            foreach (var file in options.files) {

                var dialogue = YarnSpinnerConsole.CreateDialogueForUtilities();

                // Load and compile the program
                try
                {
                    // First, we need to ensure that this file compiles.
                    dialogue.LoadFile(file);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
                {
                    YarnSpinnerConsole.Warn(string.Format(CultureInfo.CurrentCulture, "Skipping file {0} due to compilation errors.", file));
                    continue;
                }
#pragma warning restore CA1031 // Do not catch general exception types

                // Convert the program into BSON
                var compiledProgram = dialogue.GetCompiledProgram(options.format);

                var outputPath = System.IO.Path.ChangeExtension(file, "yarn.bytes");

                try {
                    System.IO.File.WriteAllBytes(outputPath, compiledProgram);
#pragma warning disable CA1031 // Do not catch general exception types
                } catch (Exception e) {
                    YarnSpinnerConsole.Error(string.Format(CultureInfo.CurrentCulture, "Error writing {0}: {1}", outputPath, e.Message));
                }
#pragma warning restore CA1031 // Do not catch general exception types



            }

            return 0;
        }
}
}

