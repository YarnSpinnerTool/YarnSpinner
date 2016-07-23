using System.Collections.Generic;
using Yarn;
using CsvHelper;

namespace YarnLocalisationTool
{

	class TableGenerator
	{
		public void GenerateTablesFromFiles (GenerateTableOptions options, List<string> files)
		{

			bool linesWereUntagged = false;

			foreach (var file in files) {
				var dialogue = new Dialogue (null);

				dialogue.LogDebugMessage = delegate(string message) {
					MainClass.Note(message);	
				};

				dialogue.LogErrorMessage = delegate(string message) {
					MainClass.Error (message);
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
					MainClass.Warn(string.Format("Untagged lines in {0}", file));
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
							MainClass.Note("Wrote " + filePath);
						}
					}					
				}

			}

			if (linesWereUntagged) {
				MainClass.Warn("Some lines were not tagged, so they weren't added to the " +
				               "string file. Use this tool's 'generate' action to add them.");
			}

		}

		string CreateCSVRow (params string[] entries) {
			return string.Join (",", entries);
		}

		string CreateCSVRow (KeyValuePair<string,string> entry) {
			return CreateCSVRow (new string[] { entry.Key, entry.Value });
		}
	}

}
