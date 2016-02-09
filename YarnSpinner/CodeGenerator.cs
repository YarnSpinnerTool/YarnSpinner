using System;
using System.Collections.Generic;

namespace Yarn
{
	internal class State {
		public int programCounter = 0;
		public string currentNode;
		public Stack<Value> stack = new Stack<Value>();

		public void PushValue(object o) {
			stack.Push (new Value(o));
		}

		public Value PopValue() {
			return stack.Pop ();
		}

		public Value PeekValue() {
			return stack.Peek ();
		}
	}

	internal class Program {
		public List<string> strings = new List<string>();

		public Dictionary<string, Node> nodes = new Dictionary<string, Node> ();


		public int RegisterString(string theString) {
			var index = strings.IndexOf (theString);
			if (index > 0)
				return index;

			// It's not in the list; append it
			strings.Add(theString);
			return strings.Count - 1;
		}

		public string GetString(int i) {
			return strings [i];
		}

		public string DumpCode() {

			var sb = new System.Text.StringBuilder ();

			foreach (var entry in nodes) {
				sb.AppendLine ("Node " + entry.Key + ":");

				int instructionCount = 0;
				foreach (var instruction in entry.Value.instructions) {
					string instructionText;

					if (instruction.operation == ByteCode.Label) {
						instructionText = instruction.ToString (this);
					} else {
						instructionText = "    " + instruction.ToString (this);
					}

					string preface;

					if (instructionCount % 5 == 0 || instructionCount == entry.Value.instructions.Count - 1) {
						preface = string.Format ("{0,6}   ", instructionCount);
					} else {
						preface = string.Format ("{0,6}   ", " ");
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

		public List<Instruction> instructions = new List<Instruction>();
	}

	internal struct Instruction {
		internal ByteCode operation;
		internal object operandA;
		internal object operandB;

		public  string ToString(Program p) {

			if (operation == ByteCode.Label) {
				return operandA + ":";
			}

			var opAString = operandA != null ? operandA.ToString () : "";
			var opBString = operandB != null ? operandB.ToString () : "";

			string comment = "";

			switch (operation) {
			case ByteCode.PushString:
			case ByteCode.RunLine:
				var text = p.GetString((int)operandA);
				comment = string.Format ("; \"{0}\"", text);
				break;
			}

			return string.Format ("{0} {1} {2} {3}", operation.ToString (), opAString, opBString, comment);
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
		JumpIfTrue,		// opA = string: label name if top of stack is not null, zero or false, jumps to that label; pops top of stack
		Pop,			// discard top of stack
		CallFunc,		// opA = string; looks up function, pops as many arguments as needed, result is pushed to stack
		Load,			// opA = name of variable to get value of and push to stack
		Store,			// opA = name of variable to store top of stack in
		Stop,			// stops execution
		RunNode			// opA = name of node to start running
	}


	internal class CodeGenerator
	{




		internal Program program { get; private set; }

		internal CodeGenerator ()
		{
			program = new Program ();
		}

		internal void CompileNode(Parser.Node node) {

			if (program.nodes.ContainsKey(node.name)) {
				throw new ArgumentException ("Duplicate node name " + node.name);
			}

			var compiledNode =  new Node();

			var startLabel = RegisterLabel ();
			Emit (compiledNode, ByteCode.Label, startLabel);

			foreach (var statement in node.statements) {
				GenerateCode (compiledNode, statement);
			}

			program.nodes [node.name] = compiledNode;
		}

		private int labelCount = 0;

		// Generates a unique label name to use
		string RegisterLabel() {
			return "L" + labelCount++;
		}

		void Emit(Node node, ByteCode code, object operandA = null, object operandB = null) {
			var instruction = new Instruction();
			instruction.operation = code;
			instruction.operandA = operandA;
			instruction.operandB = operandB;

			node.instructions.Add (instruction);
		}

		// Statements
		void GenerateCode(Node node, Parser.Statement statement) {
			switch (statement.type) {
			case Parser.Statement.Type.CustomCommand:
				GenerateCode (node, statement.customCommand);
				break;
			case Parser.Statement.Type.ShortcutOptionGroup:
				GenerateCode (node, statement.shortcutOptionGroup);
				break;
			case Parser.Statement.Type.Block:
				
				// Blocks are just groups of statements
				foreach (var blockStatement in statement.block.statements) {
					GenerateCode(node, blockStatement);
				}

				break;


			case Parser.Statement.Type.IfStatement:
				GenerateCode (node, statement.ifStatement);
				break;

			case Parser.Statement.Type.OptionStatement:
				GenerateCode (node, statement.optionStatement);
				break;

			case Parser.Statement.Type.AssignmentStatement:
				GenerateCode (node, statement.assignmentStatement);
				break;

			case Parser.Statement.Type.Line:
				GenerateCode (node, statement.line);
				break;

			default:
				throw new ArgumentOutOfRangeException ();
			}


		}

		void GenerateCode(Node node, Parser.CustomCommand statement) {

			Emit (node, ByteCode.RunCommand, statement.clientCommand);

		}

		void GenerateCode(Node node, string line) {
			var num = program.RegisterString (line);

			Emit (node, ByteCode.RunLine, num);

		}

		void GenerateCode(Node node, Parser.ShortcutOptionGroup statement) {

			throw new NotImplementedException ();
		}

		void GenerateCode(Node node, IEnumerable<Yarn.Parser.Statement> statementList) {

			foreach (var statement in statementList) {
				GenerateCode (node, statement);
			}
		}

		void GenerateCode(Node node, Parser.IfStatement statement) {
			
			foreach (var clause in statement.clauses) {
				var endOfClauseLabel = RegisterLabel ();

				if (clause.expression != null) {
					
					GenerateCode (node, clause.expression);

					Emit (node, ByteCode.PushBool, false);
					Emit (node, ByteCode.CallFunc, "==");
					Emit (node, ByteCode.JumpIfTrue, endOfClauseLabel);

				}

				GenerateCode (node, clause.statements);

				Emit (node, ByteCode.Label, endOfClauseLabel);

			}
		}

		void GenerateCode(Node node, Parser.OptionStatement statement) {
			throw new NotImplementedException ();
		}

		void GenerateCode(Node node, Parser.AssignmentStatement statement) {
			throw new NotImplementedException ();
		}

		void GenerateCode(Node node, Parser.Expression expression) {
			if (expression.type == Parser.Expression.Type.Value) {
				GenerateCode (node, expression.value);
			}
		}

		void GenerateCode(Node node, Parser.ValueNode value) {
			switch (value.value.type) {
			case Value.Type.Number:
				Emit (node, ByteCode.PushNumber, value.value.numberValue);
				break;
			case Value.Type.String:
				var id = program.RegisterString (value.value.stringValue);
				Emit (node, ByteCode.PushString, id);
				break;
			case Value.Type.Bool:
				Emit (node, ByteCode.PushBool, value.value.boolValue);
				break;
			case Value.Type.Variable:
				break;
			case Value.Type.Null:
				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}
		}




	}
}

