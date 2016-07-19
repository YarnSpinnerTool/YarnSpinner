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

		static void Error(string message) {
			Console.ForegroundColor = ConsoleColor.DarkRed;
			Console.Write ("Error: ");
			Console.ResetColor ();
			Console.WriteLine (message);
			Environment.Exit (1);
		}

		static void Warn(string message) {
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.Write ("Error: ");
			Console.ResetColor ();
			Console.WriteLine (message);
		}

		static void GenerateStringTableFromFiles(GenerateTableOptions options) {

			var files = new List<string> (options.sourceFiles);

			if (files.Count == 0) {
				Error ("No files provided.");
			}

			Console.WriteLine("Generating from files:");

			foreach (var file in files) {
				Console.WriteLine("\t" + file);
			}
		}

		static void AddLabelsToFiles (AddLabelsOptions obj)
		{
			Console.WriteLine ("Adding labels to files:");
			foreach (var file in obj.sourceFiles) {
				Console.WriteLine ("\t" + file);
			}
		}
	}
}
