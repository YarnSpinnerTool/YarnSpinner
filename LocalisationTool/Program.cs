using System;
using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace YarnLocalisationTool
{


	[Verb("generate")]
	class GenerateTableOptions {

		[Value(0, MetaName="source files", HelpText="The files to use.")]
		public IEnumerable<string> sourceFiles { get; set; } 

		[Option(HelpText="Generate verbose output.")]
		public bool verbose { get; set; }

		[Option(HelpText="The directory to place output files.", Default=".")]
		public string outputPath { get; set; }
	
	}

	[Verb("add")]
	class AddLabelsOptions {

		[Value(0)]
		public IEnumerable<string> sourceFiles { get; set; } 

	}

	class MainClass
	{
		public static void Main (string[] args)
		{
			var result = CommandLine.Parser.Default.ParseArguments<AddLabelsOptions, GenerateTableOptions> (args);

			result.WithParsed<GenerateTableOptions> (options => {
				GenerateStringTableFromFiles(options);
			});

			result.WithParsed<AddLabelsOptions> (options => {
				AddLabelsToFiles (options);
			});
		}

		public static void Error(params string[] messages) {

			foreach (var message in messages) {
				Console.ForegroundColor = ConsoleColor.DarkRed;
				Console.Write ("Error: ");
				Console.ResetColor ();
				Console.WriteLine (message);
			} 

			Environment.Exit (1);
		}

		public static void Note(params string[] messages) {

			foreach (var message in messages) {
				Console.ForegroundColor = ConsoleColor.DarkBlue;
				Console.Write ("Note: ");
				Console.ResetColor ();
				Console.WriteLine (message);
			} 


		}

		public static void Warn(params string[] messages) {
			foreach (var message in messages) {
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write ("Warning: ");
				Console.ResetColor ();
				Console.WriteLine (message);
			} 
		}

		static void CheckFileList(IEnumerable<string> paths)
		{
			var invalid = new List<string>();

			foreach (var path in paths)
			{
				var exists = System.IO.File.Exists(path);
				if (exists == false)
				{
					invalid.Add(string.Format("\"{0}\"", path));
				}
			}

			if (invalid.Count != 0)
			{

				var isMissing = new List<string>();
				var isDirectory = new List<string>();

				foreach (var entry in invalid)
				{
					if (System.IO.Directory.Exists(entry))
					{
						isDirectory.Add(entry);
					}
					else {
						isMissing.Add(entry);
					}
				}

				var messages = new List<string>();

				if (isMissing.Count > 0)
				{
					var message = string.Format("The file{0} {1} {2} not exist.",
						isMissing.Count == 1 ? "" : "s",
					                            string.Join(", ", isMissing.ToArray()),
						isMissing.Count == 1 ? "does not exist" : "do not exist"
					);
					messages.Add(message);
				}

				if (isDirectory.Count > 0)
				{
					var message = string.Format("The file{0} {1} {2}.",
						isDirectory.Count == 1 ? "" : "s",
					                            string.Join(", ", isDirectory.ToArray()),
						isDirectory.Count == 1 ? "is a directory" : "are directories"
					);
					messages.Add(message);
				}


				Error(messages.ToArray());
			}
		}
		
		static void GenerateStringTableFromFiles(GenerateTableOptions options) {

			if (System.IO.Directory.Exists(options.outputPath) == false) {
				Error(string.Format("The directory \"{0}\" does not exist.",
									options.outputPath)
					 );
			}

			var files = new List<string> (options.sourceFiles);

			if (files.Count == 0) {
				Error ("No files provided.");
			}

			CheckFileList (files);

			var tableGenerator = new TableGenerator ();
			var result = tableGenerator.GenerateTablesFromFiles (files);

			Console.WriteLine ();



			foreach (var table in result) {
				var fileName = System.IO.Path.GetFileNameWithoutExtension (table.Key);

				fileName = System.IO.Path.ChangeExtension(fileName, "csv");
				var filePath = System.IO.Path.Combine(options.outputPath, fileName);

				System.IO.File.WriteAllText(filePath, table.Value);

				if (options.verbose) {
					Note("Wrote " + filePath);
				}
			}
		}


		static void AddLabelsToFiles (AddLabelsOptions options)
		{

			var files = new List<string> (options.sourceFiles);

			if (files.Count == 0) {
				Error ("No files provided.");
			}

			Console.WriteLine ("Adding labels to files:");
			foreach (var file in files) {
				Console.WriteLine ("\t" + file);
			}

			CheckFileList (files);

		}
	}
}
