using System;
using System.Collections.Generic;
using Yarn;
using Newtonsoft.Json;

namespace Yarn
{
	class LineAdder
	{

		static internal int AddLines(AddLabelsOptions options)
		{
			foreach (var file in options.files) {

				// We can only parse json files at present
				if (System.IO.Path.GetExtension(file) != ".json") {
					YarnSpinnerConsole.Warn("Skipping non-JSON file " + file);
					continue;
				}

				Dialogue d = new Dialogue(null);

				d.LogDebugMessage = delegate (string message)
				{
					YarnSpinnerConsole.Note(message);
				};

				d.LogErrorMessage = delegate (string message)
				{
					// Warn, don't error - Erroring terminates the program
					YarnSpinnerConsole.Warn(message);
				};

				try
				{
					// First, we need to ensure that this file compiles.
					d.LoadFile(file);
				} catch {
					YarnSpinnerConsole.Warn(string.Format("Skipping file {0} due to compilation errors.", file));
					continue;
				}

				Dictionary<string, LineInfo> linesWithNoTag = new Dictionary<string, LineInfo>();

				// Filter the string info table to exclude lines that have a line code
				foreach (var entry in d.GetStringInfoTable())
				{
					if (entry.Key.StartsWith("line:") == false)
					{
						linesWithNoTag[entry.Key] = entry.Value;
					}
				}

				if (linesWithNoTag.Count == 0) {
					var message = string.Format("{0} had no untagged lines. Either they're all tagged already, or it has no localisable text.", file);
					YarnSpinnerConsole.Note(message);
					continue;
				}

				// We also need the raw NodeInfo structures contained within this file.
				// These contain the source code to what we just compiled.

				Loader.NodeInfo[] nodeInfoList;

				using (var reader = new System.IO.StreamReader(file) ) {
					nodeInfoList = d.loader.ParseInput(reader.ReadToEnd());
				}

				// Convert this list into an easier-to-index dictionary
				var nodes = new Dictionary<string, Loader.NodeInfo>();

				foreach (var node in nodeInfoList) {
					nodes[node.title] = node;

				}

				// Make a list of line codes that we already know about.
				// This list will be updated as we tag lines, to prevent collisions.
				var existingKeys = new List<string>(nodes.Keys);

				// We now have a list of all strings that do not have a string tag.
				// Add a new tag to these lines.
				foreach (var line in linesWithNoTag) {

					// TODO: There's quite a bit of redundant work done here in each loop.
					// We're unzipping and re-combining the node for EACH line. Would be better
					// to do that only once.

					// Get the node that this line is in.
					var node = nodes[line.Value.nodeName];

					// Split this node's source by newlines
					var lines = node.body.Split(new string[] { "\r\n", "\n"}, StringSplitOptions.None);

					// Get the original line
					var existingLine = lines[line.Value.lineNumber - 1];

					// Generate a new tag for this line
					var newTag = GenerateString(existingKeys);

					// Remember that we've used this tag, to prevent it from being re-used
					existingKeys.Add(newTag);

					// Re-write the line.
					var newLine = string.Format("{0} #{1}", existingLine, newTag);
					lines[line.Value.lineNumber - 1] = newLine;


					// Pack this all back up into a single string
					node.body = string.Join("\n", lines);

					// Put the updated node in the node collection.
					nodes[line.Value.nodeName] = node;

				}

				// All the nodes have been updated; save this back to disk.

				// Are we doing a dry run?
				if (options.dryRun) {
					// Then bail out at this point, before we start
					// modifying files
					YarnSpinnerConsole.Note("Would have written to file " + file);
					continue;
				}

				// Convert the nodes back into JSON
				var jsonData = JsonConvert.SerializeObject(nodes.Values, Formatting.Indented);

				// Write the file!
				using (var writer = new System.IO.StreamWriter(file)) {
					writer.Write(jsonData);
				}


			}
			return 0;
		}


		static Random random = new Random();

		// Generates a new unique line tag that is not present in 'existingKeys'.
		static string GenerateString(List<string> existingKeys) {

			string tag = null;
			bool isUnique = true;
			do
			{
				tag = String.Format("line:{0:x6}", random.Next(0x1000000));

				isUnique = existingKeys.FindIndex(i => i == tag) == -1;

			} while (isUnique == false);

			return tag;
		}
	}
}