using System.Collections.Generic;
using Yarn;
using CsvHelper;

namespace Yarn
{

	class TableGenerator
	{
		static internal int GenerateTables (GenerateTableOptions options)
		{

			YarnSpinnerConsole.CheckFileList(options.files, YarnSpinnerConsole.ALLOWED_EXTENSIONS);

			if (options.verbose && options.onlyUseTag != null) {
				YarnSpinnerConsole.Note(string.Format("Only using lines from nodes tagged \"{0}\"", options.onlyUseTag));
			}

			bool linesWereUntagged = false;

			foreach (var file in options.files) {

				// Note that we're passing in with a null library - this means
				// that all function checking will be disabled, and missing funcs
				// will not cause a compile error. If a func IS missing at runtime,
				// THAT will throw an exception.

				// We do this because this tool has no idea about any of the custom
				// functions that you might be using.
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

					// If options.onlyUseTag is set, we skip all lines in nodes that
					// don't have that tag.
					if (options.onlyUseTag != null) {

						// Find the tags for the node that this string is in
						var stringInfo = dialogue.program.lineInfo[entry.Key];
						var node = dialogue.program.nodes[stringInfo.nodeName];
						var tags = node.tags;

						// If the tags don't include the one we're looking for,
						// skip this line
						if (tags.FindIndex(i => i == options.onlyUseTag) == -1) {
							continue;
						}

					}

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

						var dir = System.IO.Path.GetDirectoryName(file);
						var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
						fileName += "_lines.csv";
						var filePath = System.IO.Path.Combine(dir, fileName);

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
