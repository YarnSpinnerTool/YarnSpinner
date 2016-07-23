using System;

using Newtonsoft.Json;

namespace Yarn
{
	public class ProgramExporter
	{
		public ProgramExporter()
		{
		}

		internal static int Export(CompileOptions options)
		{
			YarnSpinnerConsole.CheckFileList(options.files, YarnSpinnerConsole.ALLOWED_EXTENSIONS);

			foreach (var file in options.files) {

				// Note that we're passing in with a null library - this means
				// that all function checking will be disabled, and missing funcs
				// will not cause a compile error. If a func IS missing at runtime,
				// THAT will throw an exception.

				// We do this because this tool has no idea about any of the custom
				// functions that you might be using.

				var dialogue = new Dialogue(null);

				dialogue.LogDebugMessage = (string message) => YarnSpinnerConsole.Note(message);
				dialogue.LogErrorMessage = (string message) => YarnSpinnerConsole.Warn(message);

				// Load and compile the program
				try
				{
					// First, we need to ensure that this file compiles.
					dialogue.LoadFile(file);
				}
				catch
				{
					YarnSpinnerConsole.Warn(string.Format("Skipping file {0} due to compilation errors.", file));
					continue;
				}

				// Convert the program into BSON
				var compiledProgram = dialogue.GetCompiledProgram(options.format);

				var outputPath = System.IO.Path.ChangeExtension(file, "yarn.bytes");

				try {
					System.IO.File.WriteAllBytes(outputPath, compiledProgram);
				} catch (Exception e) {
					YarnSpinnerConsole.Error(string.Format("Error writing {0}: {1}", outputPath, e.Message));
				}



			}

			return 0;
		}
}
}

