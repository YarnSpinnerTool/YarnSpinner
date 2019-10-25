using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Globalization;
using static Yarn.Instruction.Types;

namespace Yarn
{
	
	public partial class Program
	{

		// When saving programs, we want to save only lines that do NOT have a line: key.
		// This is because these lines will be loaded from a string table.
		// However, because certain strings (like those used in expressions) won't have tags,
		// they won't be included in generated string tables, so we need to export them here.

		// We do this by NOT including the main strings list, and providing a property
		// that gets serialised as "strings" in the output, which includes all untagged strings.

		internal Dictionary<string, string> untaggedStrings
		{
			get
			{
				var result = new Dictionary<string, string>();
				foreach (var line in this.StringTable)
				{
					if (line.Key.StartsWith("line:", StringComparison.InvariantCulture))
					{
						continue;
					}
					result.Add(line.Key, line.Value);
				}
				return result;
			}
		}

		private int stringCount = 0;

		/// Loads a new string table into the program.
		/** The string table is merged with any existing strings,
         * with the new table taking precedence over the old.
         */
		// TODO: this information relates to the execution of the program,
		// and not to the program as stored on disk. Move this
		// functinoality to the VM.
		public void LoadStrings(Dictionary<string, string> newStrings)
		{
			foreach (var entry in newStrings)
			{
				StringTable[entry.Key] = entry.Value;
			}
		}

		public string RegisterString(string theString, string nodeName, string lineID, int lineNumber, bool localisable)
		{

			string key;

			if (lineID == null)
				key = string.Format(CultureInfo.InvariantCulture, "{0}-{1}", nodeName, stringCount++);
			else
				key = lineID;

			// It's not in the list; append it
			StringTable.Add(key, theString);

			if (localisable)
			{
				// Additionally, keep info about this string around
				var lineInfo = new LineInfo();
				lineInfo.NodeName = nodeName;
				lineInfo.LineNumber = lineNumber;
				this.LineInfo.Add(key, lineInfo);
			}

			return key;
		}

		public string GetString(string key)
		{
			string value = null;
			StringTable.TryGetValue(key, out value);
			return value;
		}

		public string DumpCode(Library l)
		{

			var sb = new System.Text.StringBuilder();

			foreach (var entry in Nodes)
			{
				sb.AppendLine("Node " + entry.Key + ":");

				int instructionCount = 0;
				foreach (var instruction in entry.Value.Instructions)
				{
					string instructionText;

					if (instruction.Opcode == OpCode.Label)
					{
						instructionText = instruction.ToString(this, l);
					}
					else
					{
						instructionText = "    " + instruction.ToString(this, l);
					}

					string preface;

					if (instructionCount % 5 == 0 || instructionCount == entry.Value.Instructions.Count - 1)
					{
						preface = string.Format(CultureInfo.InvariantCulture, "{0,6}   ", instructionCount);
					}
					else
					{
						preface = string.Format(CultureInfo.InvariantCulture, "{0,6}   ", " ");
					}

					sb.AppendLine(preface + instructionText);

					instructionCount++;
				}

				sb.AppendLine();
			}

			sb.AppendLine("String table:");

			foreach (var entry in StringTable)
			{
				var lineInfo = this.LineInfo[entry.Key];

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1} ({2}:{3})", entry.Key, entry.Value, lineInfo.NodeName, lineInfo.LineNumber));
			}

			return sb.ToString();
		}

		public string GetTextForNode(string nodeName)
		{
			return this.GetString(Nodes[nodeName].SourceTextStringID);
		}

		public IEnumerable<string> GetTagsForNode(string nodeName)
		{
			return Nodes[nodeName].Tags;
		}

		// TODO: this behaviour belongs in the VM as a "load additional program" feature, not in the Program data object
		public static Program Combine(params Program[] programs) {
			if (programs.Length == 0) {
				throw new ArgumentException(nameof(programs), "At least one program must be provided.");				
			}

			var output = new Program();

			foreach (var otherProgram in programs) {
				foreach (var otherNodeName in otherProgram.Nodes) {

					if (output.Nodes.ContainsKey(otherNodeName.Key))
					{
						throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "This program already contains a node named {0}", otherNodeName.Key));
					}

					output.Nodes[otherNodeName.Key] = otherNodeName.Value.Clone();
				}

				foreach (var otherString in otherProgram.StringTable) {

					if (output.Nodes.ContainsKey(otherString.Key))
					{
						throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "This program already contains a string with key {0}", otherString.Key));
					}

					output.StringTable[otherString.Key] = otherString.Value;			
				}				
			}
			return output;
		}

		public void Include(Program otherProgram)
		{
			
		}
	}

	public partial class Instruction
	{
		
		public string ToString(Program p, Library l)
		{

			// Labels are easy: just dump out the name
			if (Opcode == OpCode.Label)
			{
				return Operands[0].StringValue + ":";
			}

			// Generate a comment, if the instruction warrants it
			string comment = "";

			// Stack manipulation comments
			var pops = 0;
			var pushes = 0;

			switch (Opcode)
			{

				// These operations all push a single value to the stack
				case OpCode.PushBool:
				case OpCode.PushNull:
				case OpCode.PushNumber:
				case OpCode.PushString:
				case OpCode.PushVariable:
				case OpCode.ShowOptions:
					pushes = 1;
					break;

				// Functions pop 0 or more values, and pop 0 or 1
				case OpCode.CallFunc:
					var function = l.GetFunction(Operands[0].StringValue);

					pops = function.paramCount;

					if (function.returnsValue)
						pushes = 1;


					break;

				// Pop always pops a single value
				case OpCode.Pop:
					pops = 1;
					break;

				// Switching to a different node will always clear the stack
				case OpCode.RunNode:
					comment += "Clears stack";
					break;
			}

			// If we had any pushes or pops, report them

			if (pops > 0 && pushes > 0)
				comment += string.Format(CultureInfo.InvariantCulture, "Pops {0}, Pushes {1}", pops, pushes);
			else if (pops > 0)
				comment += string.Format(CultureInfo.InvariantCulture, "Pops {0}", pops);
			else if (pushes > 0)
				comment += string.Format(CultureInfo.InvariantCulture, "Pushes {0}", pushes);

			// String lookup comments
			switch (Opcode)
			{
				case OpCode.PushString:
				case OpCode.RunLine:
				case OpCode.AddOption:

					// Add the string for this option, if it has one
					if (Operands[0].StringValue != "")
					{
						var text = p.GetString(Operands[0].StringValue);
						comment += string.Format(CultureInfo.InvariantCulture, "\"{0}\"", text);
					}

					break;

			}

			if (comment != "")
			{
				comment = "; " + comment;
			}

			string opAString = Operands.Count > 0 ? Operands[0].ToString() : "";
			string opBString = Operands.Count > 1 ? Operands[1].ToString() : "";

			return string.Format(
                CultureInfo.InvariantCulture,
                "{0,-15} {1,-10} {2,-10} {3, -10}",
                Opcode.ToString(),
                opAString,
                opBString,
                comment);
		}
	}

}
