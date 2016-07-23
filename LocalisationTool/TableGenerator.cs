using System.Collections.Generic;
using Yarn;
using CsvHelper;

namespace YarnLocalisationTool
{

	class TableGenerator
	{
		public Dictionary<string,string> GenerateTablesFromFiles (List<string> files)
		{

			bool linesWereUntagged = true;

			var returnedTables = new Dictionary<string, string> ();

			foreach (var file in files) {
				Dialogue d = new Dialogue (null);

				d.LogDebugMessage = delegate(string message) {
					MainClass.Note(message);	
				};

				d.LogErrorMessage = delegate(string message) {
					MainClass.Error (message);
				};

				d.LoadFile (file);

				var stringTable = d.GetStringTable ();

				var emittedStringTable = new Dictionary<string,string> ();

				var warnUntaggedLines = false;

				foreach (var entry in stringTable) {
					if (entry.Key.StartsWith("line:") == false) {
						warnUntaggedLines = true;
					} else {
						emittedStringTable [entry.Key] = entry.Value;
					}
				}

				if (warnUntaggedLines) {
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

						returnedTables[file] = w.ToString();
					}					
				}

			}

			if (linesWereUntagged) {
				MainClass.Warn("Some lines were not tagged, so they weren't added to the " +
				               "string file. Use this tool's 'generate' action to add them.");
			}

			return returnedTables;

		}

		string CreateCSVRow (params string[] entries) {
			return string.Join (",", entries);
		}

		string CreateCSVRow (KeyValuePair<string,string> entry) {
			return CreateCSVRow (new string[] { entry.Key, entry.Value });
		}
	}


	

	
}
