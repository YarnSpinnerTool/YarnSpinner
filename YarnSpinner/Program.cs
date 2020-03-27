using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Globalization;
using static Yarn.Instruction.Types;

namespace Yarn
{
	/// <summary>
	/// Information about a string. Stored inside a string table, which is
	/// produced from the Compiler.
	/// </summary>
	public struct StringInfo {
            public string text;
            public string nodeName;
            public int lineNumber;
			public string fileName;
			public bool isImplicitTag;
			public string[] metadata;

            public StringInfo(string text, string fileName, string nodeName, int lineNumber, bool isImplicitTag, string[] metadata)
            {
                this.text = text;
                this.nodeName = nodeName;
                this.lineNumber = lineNumber;
				this.fileName = fileName;
				this.isImplicitTag = isImplicitTag;

				if (metadata != null) {
					this.metadata = metadata;
				} else {
					this.metadata = new string[] {};
				}
				
            }
        }
	
	/// <summary>
	/// A compiled Yarn program.
	/// </summary>
	public partial class Program
	{

		
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

					instructionText = "    " + instruction.ToString(this, l);
					
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

			
			return sb.ToString();
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
			}
			return output;
		}

		public void Include(Program otherProgram)
		{
			
		}
	}

	/// <summary>
	/// An instruction in a Yarn Program.
	/// </summary>
	public partial class Instruction
	{
		
		public string ToString(Program p, Library l)
		{

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
				case OpCode.PushFloat:
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
						comment += string.Format(CultureInfo.InvariantCulture, "\"{0}\"", Operands[0].StringValue);
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
