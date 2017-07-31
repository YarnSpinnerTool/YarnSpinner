using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Yarn
{
	// An exception representing something going wrong during parsing
	[Serializable]
	internal class ParseException : Exception
	{

		internal int lineNumber = 0;

		internal static ParseException Make(Token foundToken, params TokenType[] expectedTypes)
		{

			var lineNumber = foundToken.lineNumber + 1;

			var expectedTypeNames = new List<String>();
			foreach (var type in expectedTypes)
			{
				expectedTypeNames.Add(type.ToString());
			}

			string possibleValues = string.Join(",", expectedTypeNames.ToArray());
			string message = string.Format("Line {0}:{1}: Expected {2}, but found {3}",
										   lineNumber,
										   foundToken.columnNumber,
										   possibleValues,
										   foundToken.type.ToString()
										   );
			var e = new ParseException(message);
			e.lineNumber = lineNumber;
			return e;
		}

		internal static ParseException Make(Token mostRecentToken, string message)
		{
			var lineNumber = mostRecentToken.lineNumber + 1;
			string theMessage = string.Format("Line {0}:{1}: {2}",
								 lineNumber,
								mostRecentToken.columnNumber,
								 message);
			var e = new ParseException(theMessage);
			e.lineNumber = lineNumber;
			return e;
		}

        internal static ParseException Make(Antlr4.Runtime.ParserRuleContext context, string message)
        {
            int line = context.Start.Line;

            // getting the text that has the issue inside
			int start = context.Start.StartIndex;
			int end = context.Stop.StopIndex;
            string body = context.Start.InputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, end));

            string theMessage = String.Format("Error on line {0}\n{1}\n{2}",line,body,message);

            var e = new ParseException(theMessage);
            e.lineNumber = line;
            return e;
        }

		internal ParseException(string message) : base(message) { }

	}

	internal struct LineInfo
	{
		public int lineNumber;
		public string nodeName;

		public LineInfo(string nodeName, int lineNumber)
		{
			this.nodeName = nodeName;
			this.lineNumber = lineNumber;
		}
	}

	[JsonObject(MemberSerialization.OptIn)] // properties must opt-in to JSON serialization
	internal class Program
	{

		internal Dictionary<string, string> strings = new Dictionary<string, string>();
		internal Dictionary<string, LineInfo> lineInfo = new Dictionary<string, LineInfo>();

		[JsonProperty]
		internal Dictionary<string, Node> nodes = new Dictionary<string, Node>();

		// When saving programs, we want to save only lines that do NOT have a line: key.
		// This is because these lines will be loaded from a string table.
		// However, because certain strings (like those used in expressions) won't have tags,
		// they won't be included in generated string tables, so we need to export them here.

		// We do this by NOT including the main strings list, and providing a property
		// that gets serialised as "strings" in the output, which includes all untagged strings.

		[JsonProperty("strings")]
		internal Dictionary<string, string> untaggedStrings
		{
			get
			{
				var result = new Dictionary<string, string>();
				foreach (var line in strings)
				{
					if (line.Key.StartsWith("line:"))
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
		public void LoadStrings(Dictionary<string, string> newStrings)
		{
			foreach (var entry in newStrings)
			{
				strings[entry.Key] = entry.Value;
			}
		}

		public string RegisterString(string theString, string nodeName, string lineID, int lineNumber, bool localisable)
		{

			string key;

			if (lineID == null)
				key = string.Format("{0}-{1}", nodeName, stringCount++);
			else
				key = lineID;

			// It's not in the list; append it
			strings.Add(key, theString);

			if (localisable)
			{
				// Additionally, keep info about this string around
				lineInfo.Add(key, new LineInfo(nodeName, lineNumber));
			}

			return key;
		}

		public string GetString(string key)
		{
			string value = null;
			strings.TryGetValue(key, out value);
			return value;
		}

		public string DumpCode(Library l)
		{

			var sb = new System.Text.StringBuilder();

			foreach (var entry in nodes)
			{
				sb.AppendLine("Node " + entry.Key + ":");

				int instructionCount = 0;
				foreach (var instruction in entry.Value.instructions)
				{
					string instructionText;

					if (instruction.operation == ByteCode.Label)
					{
						instructionText = instruction.ToString(this, l);
					}
					else
					{
						instructionText = "    " + instruction.ToString(this, l);
					}

					string preface;

					if (instructionCount % 5 == 0 || instructionCount == entry.Value.instructions.Count - 1)
					{
						preface = string.Format("{0,6}   ", instructionCount);
					}
					else
					{
						preface = string.Format("{0,6}   ", " ");
					}

					sb.AppendLine(preface + instructionText);

					instructionCount++;
				}

				sb.AppendLine();
			}

			sb.AppendLine("String table:");

			foreach (var entry in strings)
			{
				var lineInfo = this.lineInfo[entry.Key];

                sb.AppendLine(string.Format("{0}: {1} ({2}:{3})", entry.Key, entry.Value, lineInfo.nodeName, lineInfo.lineNumber));
			}

			return sb.ToString();
		}

		public string GetTextForNode(string nodeName)
		{
			return this.GetString(nodes[nodeName].sourceTextStringID);
		}

		public void Include(Program otherProgram)
		{
			foreach (var otherNodeName in otherProgram.nodes)
			{

				if (nodes.ContainsKey(otherNodeName.Key))
				{
					throw new InvalidOperationException(string.Format("This program already contains a node named {0}", otherNodeName.Key));
				}

				nodes[otherNodeName.Key] = otherNodeName.Value;
			}

			foreach (var otherString in otherProgram.strings)
			{

				if (nodes.ContainsKey(otherString.Key))
				{
					throw new InvalidOperationException(string.Format("This program already contains a string with key {0}", otherString.Key));
				}

				strings[otherString.Key] = otherString.Value;
			}
		}
	}

	internal class Node
	{

		public List<Instruction> instructions = new List<Instruction>();

		public string name;

		/// the entry in the program's string table that contains
		/// the original text of this node; null if this is not available
		public string sourceTextStringID = null;

		public Dictionary<string, int> labels = new Dictionary<string, int>();

		public List<string> tags;
	}

	struct Instruction
	{
		public ByteCode operation;
		public object operandA;
		public object operandB;

		public string ToString(Program p, Library l)
		{

			// Labels are easy: just dump out the name
			if (operation == ByteCode.Label)
			{
				return operandA + ":";
			}

			// Convert the operands to strings
			var opAString = operandA != null ? operandA.ToString() : "";
			var opBString = operandB != null ? operandB.ToString() : "";

			// Generate a comment, if the instruction warrants it
			string comment = "";

			// Stack manipulation comments
			var pops = 0;
			var pushes = 0;

			switch (operation)
			{

				// These operations all push a single value to the stack
				case ByteCode.PushBool:
				case ByteCode.PushNull:
				case ByteCode.PushNumber:
				case ByteCode.PushString:
				case ByteCode.PushVariable:
				case ByteCode.ShowOptions:
					pushes = 1;
					break;

				// Functions pop 0 or more values, and pop 0 or 1
				case ByteCode.CallFunc:
					var function = l.GetFunction((string)operandA);

					pops = function.paramCount;

					if (function.returnsValue)
						pushes = 1;


					break;

				// Pop always pops a single value
				case ByteCode.Pop:
					pops = 1;
					break;

				// Switching to a different node will always clear the stack
				case ByteCode.RunNode:
					comment += "Clears stack";
					break;
			}

			// If we had any pushes or pops, report them

			if (pops > 0 && pushes > 0)
				comment += string.Format("Pops {0}, Pushes {1}", pops, pushes);
			else if (pops > 0)
				comment += string.Format("Pops {0}", pops);
			else if (pushes > 0)
				comment += string.Format("Pushes {0}", pushes);

			// String lookup comments
			switch (operation)
			{
				case ByteCode.PushString:
				case ByteCode.RunLine:
				case ByteCode.AddOption:

					// Add the string for this option, if it has one
					if ((string)operandA != null)
					{
						var text = p.GetString((string)operandA);
						comment += string.Format("\"{0}\"", text);
					}

					break;

			}

			if (comment != "")
			{
				comment = "; " + comment;
			}

			return string.Format("{0,-15} {1,-10} {2,-10} {3, -10}", operation.ToString(), opAString, opBString, comment);
		}
	}

	internal enum ByteCode
	{

		/// opA = string: label name
		Label,
		/// opA = string: label name
		JumpTo,
		/// peek string from stack and jump to that label
		Jump,
		/// opA = int: string number
		RunLine,
		/// opA = string: command text
		RunCommand,
		/// opA = int: string number for option to add
		AddOption,
		/// present the current list of options, then clear the list; most recently selected option will be on the top of the stack
		ShowOptions,
		/// opA = int: string number in table; push string to stack
		PushString,
		/// opA = float: number to push to stack
		PushNumber,
		/// opA = int (0 or 1): bool to push to stack
		PushBool,
		/// pushes a null value onto the stack
		PushNull,
		/// opA = string: label name if top of stack is not null, zero or false, jumps to that label
		JumpIfFalse,
		/// discard top of stack
		Pop,
		/// opA = string; looks up function, pops as many arguments as needed, result is pushed to stack
		CallFunc,
		/// opA = name of variable to get value of and push to stack
		PushVariable,
		/// opA = name of variable to store top of stack in
		StoreVariable,
		/// stops execution
		Stop,
		/// run the node whose name is at the top of the stack
		RunNode

	}
}
