using System.Collections.Generic;
using Yarn;

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
					MainClass.Debug(message);	
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

				var sb = new System.Text.StringBuilder ();

				sb.AppendLine (CreateCSVRow ("Key", "Line"));

				foreach (var entry in emittedStringTable) {
					sb.AppendLine (CreateCSVRow (entry));
				}

				returnedTables [file] = sb.ToString ();


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
