using System;
using System.Collections.Generic;

namespace Yarn
{
	internal class State {
		int programCounter = 0;
		string currentNode;
		Stack<Value> stack = new Stack<Value>();
	}

	internal class CodeGenerator
	{

		internal class Program {
			public List<string> strings = new List<string>();

			public Dictionary<string, Node> nodes = new Dictionary<string, Node> ();

			public string DumpCode() {

				var sb = new System.Text.StringBuilder ();

				foreach (var entry in nodes) {
					sb.AppendLine ("Node " + entry.Key + ":");

					int instructionCount = 0;
					foreach (var instruction in entry.Value.instructions) {
						var instructionText = instruction.ToString ();

						string preface;

						if (instructionCount % 5 == 0 || instructionCount == entry.Value.instructions.Length - 1) {
							preface = string.Format ("{0,6} ", instructionCount);
						} else {
							preface = string.Format ("{0,6} ", " ");
						}

						sb.AppendLine (preface + instructionText);

						instructionCount++;
					}

					sb.AppendLine ();
				}

				sb.AppendLine ("String table:");

				int stringCount = 0;
				foreach (var entry in strings) {
					sb.AppendLine(string.Format("{0, 4}: {1}", stringCount, entry));
					stringCount++;
				}

				return sb.ToString ();
			}

		}

		internal class Node {
			
			public Instruction[] instructions;
		}

		internal struct Instruction {
			internal ByteCode operation;
			internal object operandA;
			internal object operandB;

			public override string ToString() {

				var opAString = operandA != null ? operandA.ToString () : "";
				var opBString = operandB != null ? operandB.ToString () : "";

				return string.Format ("{0} {1} {2}", operation.ToString (), opAString, opBString);
			}
		}

		internal enum ByteCode {
			Label,			// opA = string: label name
			Jump,			// opA = string: label name
			RunLine,		// opA = int: string number
			RunCommand,		// opA = string: command text
			PushString,		// opA = int: string number in table; push string to stack
			PushNumber,		// opA = float: number to push to stack
			PushBool,		// opA = int (0 or 1): bool to push to stack
			JumpIfTrue,		// opA = string: label nameif top of stack is not null, zero or false, ju
			Pop,			// discard top of stack
			CallFunc,		// opA = string; looks up function, pops as many arguments as needed, result is pushed to stack
			Load,			// opA = name of variable to get value of and push to stack
			Store,			// opA = name of variable to store top of stack in
		}



		internal Program program { get; private set; }

		internal CodeGenerator ()
		{
			program = new Program ();
		}

		internal IEnumerable<Instruction> GenerateCode(Parser.Node node) {

			if (program.nodes.ContainsKey(node.name)) {
				throw new ArgumentException ("Duplicate node name " + node.name);
			}

			var compiledNode =  new Node();

			var instructions = new List<Instruction> ();

			foreach (var statement in node.statements) {
				var statementInstructions = GenerateCode (statement);
				instructions.AddRange (statementInstructions);
			}

			compiledNode.instructions = instructions.ToArray ();

			program.nodes [node.name] = compiledNode;

			return compiledNode.instructions;
		}

		int RegisterString(string theString) {
			var index = program.strings.IndexOf (theString);
			if (index > 0)
				return index;

			// It's not in the list; append it
			program.strings.Add(theString);
			return program.strings.Count - 1;
		}


		private int labelCount = 0;

		// Generates a unique label name to use
		string RegisterLabel() {
			return "L" + labelCount++;
		}

		void Emit(Node node, ByteCode code, object operandA = null, object operandB) {
			
		}

		// Statements
		IEnumerable<Instruction> GenerateCode(Parser.Statement statement) {
			switch (statement.type) {
			case Parser.Statement.Type.CustomCommand:
				return GenerateCode (statement.customCommand);
			case Parser.Statement.Type.ShortcutOptionGroup:
				return GenerateCode (statement.shortcutOptionGroup);
			case Parser.Statement.Type.Block:
				
				// Blocks are just groups of statements
				var blockInstructions = new List<Instruction>();
				foreach (var blockStatement in statement.block.statements) {
					blockInstructions.AddRange(GenerateCode(blockStatement));
				}
				return blockInstructions;

			case Parser.Statement.Type.IfStatement:
				return GenerateCode (statement.ifStatement);
			case Parser.Statement.Type.OptionStatement:
				return GenerateCode (statement.optionStatement);

			case Parser.Statement.Type.AssignmentStatement:
				return GenerateCode (statement.assignmentStatement);

			case Parser.Statement.Type.Line:
				return GenerateCode (statement.line);

			default:
				throw new ArgumentOutOfRangeException ();
			}


		}

		IEnumerable<Instruction> GenerateCode(Parser.CustomCommand statement) {

			Instruction i = new Instruction();

			i.operation = ByteCode.RunCommand;
			i.operandA = statement.clientCommand;

			return new Instruction[] { i };
		}

		IEnumerable<Instruction> GenerateCode(string line) {
			var num = RegisterString (line);
			Instruction i = new Instruction ();
			i.operation = ByteCode.RunLine;
			i.operandA = num;
			return new Instruction[] { i };
		}

		IEnumerable<Instruction> GenerateCode(Parser.ShortcutOptionGroup statement) {

			throw new NotImplementedException ();
		}

		IEnumerable<Instruction> GenerateCode(IEnumerable<Yarn.Parser.Statement> statementList) {
			var instructions = new List<Instruction> ();

			foreach (var statement in statementList) {
				var statementInstructions = GenerateCode (statement);
				instructions.AddRange (statementInstructions);
			}

			return instructions;
		}

		IEnumerable<Instruction> GenerateCode(Parser.IfStatement statement) {
			var instructions = new List<Instruction> ();

			foreach (var clause in statement.clauses) {
				if (clause.expression != null) {
					var endOfClauseLabel = RegisterLabel ();

				}
			}
		}

		IEnumerable<Instruction> GenerateCode(Parser.OptionStatement statement) {
			throw new NotImplementedException ();
		}

		IEnumerable<Instruction> GenerateCode(Parser.AssignmentStatement statement) {
			throw new NotImplementedException ();
		}

		IEnumerable<Instruction> GenerateCode(Parser.Expression expression) {
			if (expression.type == Parser.Expression.Type.Value) {
				return GenerateCode (expression.value);
			}
		}

		IEnumerable<Instruction> GenerateCode(Parser.ValueNode value) {
			
		}




	}
}

