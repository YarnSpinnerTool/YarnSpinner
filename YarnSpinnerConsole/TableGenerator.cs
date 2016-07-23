using System.Collections.Generic;
using Yarn;
using CsvHelper;

namespace Yarn
{

	class TableGenerator
	{
		static internal int GenerateTables (GenerateTableOptions options)
		{

			bool linesWereUntagged = false;

			foreach (var file in options.files) {
				var dialogue = new Dialogue (null);

				dialogue.LogDebugMessage = delegate(string message) {
					YarnSpinnerConsole.Note(message);	
				};

				dialogue.LogErrorMessage = delegate(string message) {
					YarnSpinnerConsole.Error (message);
				};

				dialogue.LoadFile (file);

				var stringTable = dialogue.GetStringTable ();

				var emittedStringTable = new Dictionary<string,string> ();

				var anyLinesAreUntagged = false;

				foreach (var entry in stringTable) {
					if (entry.Key.StartsWith("line:") == false) {
						anyLinesAreUntagged = true;
					} else {
						emittedStringTable [entry.Key] = entry.Value;
					}
				}

				if (anyLinesAreUntagged) {
					YarnSpinnerConsole.Warn(string.Format("Untagged lines in {0}", file));
					linesWereUntagged = true;
				}

				// Generate the CSV

				using (var w = new System.IO.StringWriter()) {
					using (var csv = new CsvWriter(w)) {

						csv.WriteHeader<LocalisedLine>();

						foreach (var entry in emittedStringTable)
						{

							var l = new LocalisedLine();
							l.LineCode = entry.Key;
							l.LineText = entry.Value;
							l.Comment = "";

							csv.WriteRecord(l);
						}

						var filePath = System.IO.Path.ChangeExtension(file, "csv");

						System.IO.File.WriteAllText(filePath, w.ToString());

						if (options.verbose)
						{
							YarnSpinnerConsole.Note("Wrote " + filePath);
						}
					}					
				}

			}

			if (linesWereUntagged) {
				YarnSpinnerConsole.Warn("Some lines were not tagged, so they weren't added to the " +
				               "string file. Use this tool's 'generate' action to add them.");
			}

			return 0;

		}

		string CreateCSVRow (params string[] entries) {
			return string.Join (",", entries);
		}

		string CreateCSVRow (KeyValuePair<string,string> entry) {
			return CreateCSVRow (new string[] { entry.Key, entry.Value });
		}
	}

}
