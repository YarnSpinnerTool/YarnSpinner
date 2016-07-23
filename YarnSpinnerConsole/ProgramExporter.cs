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
				var outputPath = System.IO.Path.ChangeExtension(file, "yarn.bytes");

				var outputStream = new System.IO.FileStream(outputPath, System.IO.FileMode.OpenOrCreate);

				try {
					using (var bsonWriter = new Newtonsoft.Json.Bson.BsonWriter(outputStream))
					{
						JsonSerializer s = new JsonSerializer();
						s.Serialize(bsonWriter, dialogue.program);

						if (options.verbose)
						{
							YarnSpinnerConsole.Note(string.Format("Wrote {0}", outputPath));
						}
					}
				} catch (Exception e) {
					YarnSpinnerConsole.Error(string.Format("Error writing {0}: {1}", outputPath, e.Message));
				}



			}

			return 0;
		}
}
}

