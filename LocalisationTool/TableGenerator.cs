using System.Collections.Generic;
using Yarn;
using CsvHelper;

namespace YarnLocalisationTool
{

	class TableGenerator
	{
		public Dictionary<string,string> GenerateTablesFromFiles (List<string> files)
		{

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
					MainClass.Warn("In \"" + file + "\":");
					MainClass.Warn("Not all lines and options have a #line: tag. " +
						"They will not be present in the string table.");
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
