using System;
using System.Collections.Generic;

namespace Yarn.Analysis
{
	public class Diagnosis {
		
		public string message;
		public string nodeName;
		public int lineNumber;
		public int columnNumber;

		public enum Severity {
			Error,
			Warning,
			Note
		}
		public Severity severity;

		public Diagnosis (string message, Severity severity, string nodeName = null, int lineNumber = -1, int columnNumber = -1)
		{
			this.message = message;
			this.nodeName = nodeName;
			this.lineNumber = lineNumber;
			this.columnNumber = columnNumber;
			this.severity = severity;
		}

		public override string ToString ()
		{
			return ToString (showSeverity: false);
		}

		public string ToString (bool showSeverity)
		{

			string contextLabel = "";

			if (showSeverity) {
				switch (severity) {
				case Severity.Error:
					contextLabel = "ERROR: ";
					break;
				case Severity.Warning:
					contextLabel = "WARNING: ";
					break;
				case Severity.Note:
					contextLabel = "Note: ";
					break;
				default:
					throw new ArgumentOutOfRangeException ();
				}
			}

			if (this.nodeName != null) {

				contextLabel += this.nodeName;
				if (this.lineNumber != -1) {
					contextLabel += string.Format (": {0}", this.lineNumber);

					if (this.columnNumber != -1) {
						contextLabel += string.Format (":{0}", this.columnNumber);
					}
				}
			} 

			string message;

			if (string.IsNullOrEmpty(contextLabel)) {
				message = this.message;
			} else {
				message = string.Format ("{0}: {1}", contextLabel, this.message);
			}

			return message;
		}
		

	}

	public interface Analyser {
		IEnumerable<Diagnosis> Diagnose();
	}

	internal abstract class ASTAnalyser : Analyser {
		internal Yarn.Parser.Node node;
		public ASTAnalyser(Yarn.Parser.Node node) {
			this.node = node;
		}

		public abstract IEnumerable<Diagnosis> Diagnose ();
	}

	internal abstract class CompiledProgramAnalyser : Analyser {

		internal Yarn.Program program;

		public CompiledProgramAnalyser(Yarn.Program program) {
			this.program = program;
		}

		public abstract IEnumerable<Diagnosis> Diagnose ();
	}

	internal class UnusedVariableChecker : CompiledProgramAnalyser {

		public UnusedVariableChecker (Program program) : base (program)
		{
		}

		public override IEnumerable<Diagnosis> Diagnose ()
		{
			var readVariables = new HashSet<string> ();
			var writtenVariables = new HashSet<string> ();

			// In each node, find all reads and writes to variables
			foreach (var nodeInfo in this.program.nodes) {

				var nodeName = nodeInfo.Key;
				var theNode = nodeInfo.Value;

				foreach (var instruction in theNode.instructions) {

					switch (instruction.operation) {
					case ByteCode.PushVariable:
						readVariables.Add ((string)instruction.operandA);
						break;
					case ByteCode.StoreVariable:
						writtenVariables.Add ((string)instruction.operandA);
						break;
					}
				}
			}

			// Exclude read variables that are also written
			var readOnlyVariables = new HashSet<string> (readVariables);
			readOnlyVariables.ExceptWith (writtenVariables);

			// Exclude written variables that are also read
			var writeOnlyVariables = new HashSet<string> (writtenVariables);
			writeOnlyVariables.ExceptWith (readVariables);

			// Generate diagnoses
			var diagnoses = new List<Diagnosis>();

			foreach (var readOnlyVariable in readOnlyVariables) {
				var message = string.Format ("Variable {0} is read from, but never assigned", readOnlyVariable);
				diagnoses.Add(new Diagnosis (message, Diagnosis.Severity.Warning));
			}

			foreach (var writeOnlyVariable in writeOnlyVariables) {
				var message = string.Format ("Variable {0} is assigned, but never read from", writeOnlyVariable);
				diagnoses.Add(new Diagnosis (message, Diagnosis.Severity.Warning));
			}

			return diagnoses;

		}

	}

}

