using System.Collections.Generic;
using Yarn;
using CsvHelper;
using System.Globalization;
using System;

namespace Yarn
{

    static class TableGenerator
    {
        static internal int GenerateTables (GenerateTableOptions options)
        {

            YarnSpinnerConsole.CheckFileList(options.files, YarnSpinnerConsole.ALLOWED_EXTENSIONS);

            if (options.verbose && options.onlyUseTag != null) {
                YarnSpinnerConsole.Note(string.Format(CultureInfo.CurrentCulture, "Only using lines from nodes tagged \"{0}\"", options.onlyUseTag));
            }

            bool linesWereUntagged = false;

            foreach (var file in options.files) {

                var dialogue = YarnSpinnerConsole.CreateDialogueForUtilities();

                dialogue.LoadFile (file);

                stringTable = dialogue.GetStringTable ();

                var emittedStringTable = new Dictionary<string,string> ();

                var anyLinesAreUntagged = false;

                foreach (var entry in stringTable) {

                    // If options.onlyUseTag is set, we skip all lines in nodes that
                    // don't have that tag.
                    if (options.onlyUseTag != null) {

                        // Find the tags for the node that this string is in
                        LineInfo stringInfo;

                        try {
                            stringInfo = dialogue.program.lineInfo[entry.Key];
                        } catch (KeyNotFoundException) {
                            YarnSpinnerConsole.Error(string.Format(CultureInfo.CurrentCulture, "{0}: lineInfo table does not contain an entry for line {1} (\"{2}\")", file, entry.Key, entry.Value));
                            return 1;
                        }

                        Node node;

                        try {
                            node = dialogue.program.nodes[stringInfo.nodeName];
                        } catch (KeyNotFoundException) {
                            YarnSpinnerConsole.Error(string.Format(CultureInfo.CurrentCulture, "{0}: Line {1}'s lineInfo claims that the line originates in node {2}, but this node is not present in this program.", file, entry.Key, stringInfo.nodeName));
                            return 1;
                        }


                        var tags = node.tags;

                        // If the tags don't include the one we're looking for,
                        // skip this line
                        if (tags.FindIndex(i => i == options.onlyUseTag) == -1) {
                            continue;
                        }

                    }

                    if (entry.Key.StartsWith("line:", StringComparison.InvariantCulture) == false) {
                        anyLinesAreUntagged = true;
                    } else {
                        emittedStringTable [entry.Key] = entry.Value;
                    }
                }

                if (anyLinesAreUntagged) {
                    YarnSpinnerConsole.Warn(string.Format(CultureInfo.CurrentCulture, "Untagged lines in {0}", file));
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

        static string CreateCSVRow (params string[] entries) {
            return string.Join (",", entries);
        }

        static string CreateCSVRow (KeyValuePair<string,string> entry) {
            return CreateCSVRow (new string[] { entry.Key, entry.Value });
        }
    }

}
