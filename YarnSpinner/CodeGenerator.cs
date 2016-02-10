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

		public string DumpCode(Library l) {

			var sb = new System.Text.StringBuilder ();

			foreach (var entry in nodes) {
				sb.AppendLine ("Node " + entry.Key + ":");

				int instructionCount = 0;
				foreach (var instruction in entry.Value.instructions) {
					string instructionText;

					if (instruction.operation == ByteCode.Label) {
						instructionText = instruction.ToString (this, l);
					} else {
						instructionText = "    " + instruction.ToString (this, l);
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

		public  string ToString(Program p, Library l) {

			if (operation == ByteCode.Label) {
				return operandA + ":";
			}

			var opAString = operandA != null ? operandA.ToString () : "";
			var opBString = operandB != null ? operandB.ToString () : "";

			string comment = "";

			// Stack manipulation comments
			var pops = 0;
			var pushes = 0;

			switch (operation) {

			case ByteCode.PushBool:
			case ByteCode.PushNull:
			case ByteCode.PushNumber:
			case ByteCode.PushString:
			case ByteCode.PushVariable:
			case ByteCode.ShowOptions:
				pushes = 1;
				break;

			case ByteCode.CallFunc:
				var function = l.GetFunction ((string)operandA);

				pops = function.paramCount;

				if (function.returnsValue)
					pushes = 1;
				

				break;
			
			case ByteCode.Pop:
				pops = 1;
				break;
			}

			if (pops > 0 || pushes > 0)
				comment += string.Format ("Pops {0}, Pushes {1}", pops, pushes);

			// String lookup comments
			switch (operation) {
			case ByteCode.PushString:
			case ByteCode.RunLine:
			case ByteCode.AddOption:
				var text = p.GetString((int)operandA);
				comment += string.Format ("\"{0}\"", text);
				break;
			
			}

			if (comment != "") {
				comment = "; " + comment;
			}

			return string.Format ("{0,-15} {1,-10} {2,-10} {3, -10}", operation.ToString (), opAString, opBString, comment);
		}
	}

	internal enum ByteCode {
		
		Label,			    // opA = string: label name
		JumpTo,			    // opA = string: label name
		Jump,				// peek string from stack and jump to that label
		RunLine,		    // opA = int: string number
		RunCommand,		    // opA = string: command text
		AddOption,		    // opA = int: string number for option to add
		ShowOptions,	    // present the current list of options, then clear the list; most recently selected option will be on the top of the stack
		PushString,		    // opA = int: string number in table; push string to stack
		PushNumber,		    // opA = float: number to push to stack
		PushBool,		    // opA = int (0 or 1): bool to push to stack
		PushNull,		    // pushes a null value onto the stack
		JumpIfFalse,	    // opA = string: label name if top of stack is not null, zero or false, jumps to that label
		Pop,			    // discard top of stack
		CallFunc,		    // opA = string; looks up function, pops as many arguments as needed, result is pushed to stack
		PushVariable,			    // opA = name of variable to get value of and push to stack
		StoreVariable,			    // opA = name of variable to store top of stack in
		Stop,			    // stops execution
		RunNode			    // run the node whose name is at the top of the stack

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

			// If this compiled node has no AddOption instructions, then stop
			var hasOptions = false;
			foreach (var instruction in compiledNode.instructions) {
				if (instruction.operation == ByteCode.AddOption) {
					hasOptions = true;
					break;
				}
			}

			if (hasOptions == false) {
				Emit (compiledNode, ByteCode.Stop);
			} else {
				Emit (compiledNode, ByteCode.ShowOptions);
				Emit (compiledNode, ByteCode.RunNode);
			}


			program.nodes [node.name] = compiledNode;
		}

		private int labelCount = 0;

		// Generates a unique label name to use
		string RegisterLabel(string commentary = null) {
			return "L" + labelCount++ + commentary;
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

			if (statement.clientCommand == "stop") {
				Emit (node, ByteCode.Stop);
			} else {
				Emit (node, ByteCode.RunCommand, statement.clientCommand);
			}

		}

		void GenerateCode(Node node, string line) {
			var num = program.RegisterString (line);

			Emit (node, ByteCode.RunLine, num);

		}

		void GenerateCode(Node node, Parser.ShortcutOptionGroup statement) {

			var endOfGroupLabel = RegisterLabel ("group_end");

			var labels = new List<string> ();

			int optionCount = 0;
			foreach (var shortcutOption in statement.options) {

				var optionDestinationLabel = RegisterLabel ("option_" + (optionCount+1));
				labels.Add (optionDestinationLabel);

				string endOfClauseLabel = null;

				if (shortcutOption.condition != null) {
					endOfClauseLabel = RegisterLabel ("conditional_"+optionCount);
					GenerateCode (node, shortcutOption.condition);

					Emit (node, ByteCode.JumpIfFalse, endOfClauseLabel);
				}

				var labelStringID = program.RegisterString (shortcutOption.label);

				Emit (node, ByteCode.AddOption, labelStringID, optionDestinationLabel);

				if (shortcutOption.condition != null) {
					Emit (node, ByteCode.Label, endOfClauseLabel);
					Emit (node, ByteCode.Pop);
				}

				optionCount++;
			}

			Emit (node, ByteCode.ShowOptions);

			Emit (node, ByteCode.Jump);

			optionCount = 0;
			foreach (var shortcutOption in statement.options) {

				Emit (node, ByteCode.Label, labels [optionCount]);

				if (shortcutOption.optionNode != null)
					GenerateCode (node, shortcutOption.optionNode.statements);

				Emit (node, ByteCode.JumpTo, endOfGroupLabel);

				optionCount++;

			}

			// reached the end of the option group
			Emit (node, ByteCode.Label, endOfGroupLabel);

			// clean up after the jump
			Emit (node, ByteCode.Pop);

			// generate everything after the option group
			GenerateCode (node, statement.epilogue.statements);


		}

		void GenerateCode(Node node, IEnumerable<Yarn.Parser.Statement> statementList) {

			if (statementList == null)
				return;

			foreach (var statement in statementList) {
				GenerateCode (node, statement);
			}
		}

		void GenerateCode(Node node, Parser.IfStatement statement) {

			// We'll jump to this label at the end of every clause
			var endOfIfStatementLabel = RegisterLabel ("endif");

			foreach (var clause in statement.clauses) {
				var endOfClauseLabel = RegisterLabel ("skipclause");

				if (clause.expression != null) {
					
					GenerateCode (node, clause.expression);

					Emit (node, ByteCode.JumpIfFalse, endOfClauseLabel);

				}

				GenerateCode (node, clause.statements);

				Emit (node, ByteCode.JumpTo, endOfIfStatementLabel);

				if (clause.expression != null) {
					Emit (node, ByteCode.Label, endOfClauseLabel);
				}
				// Clean up the stack by popping the expression that was tested earlier
				if (clause.expression != null) {
					Emit (node, ByteCode.Pop);
				}
			}

			Emit (node, ByteCode.Label, endOfIfStatementLabel);
		}

		void GenerateCode(Node node, Parser.OptionStatement statement) {

			var stringID = -1;

			if (statement.label != null) {
				stringID = program.RegisterString (statement.label);
			}

			Emit (node, ByteCode.AddOption, stringID, statement.destination);

		}

		void GenerateCode(Node node, Parser.AssignmentStatement statement) {

			// Is it a straight assignment?
			if (statement.operation == TokenType.EqualToOrAssign) {
				// Evaluate the expression, which will result in a value
				// on the stack
				GenerateCode (node, statement.valueExpression);

				// Stack now contains [destinationValue]
			} else {

				// It's a combined operation-plus-assignment

				// Get the current value of the variable
				Emit(node, ByteCode.PushVariable, statement.destinationVariableName);

				// Evaluate the expression, which will result in a value
				// on the stack
				GenerateCode (node, statement.valueExpression);

				// Stack now contains [currentValue, expressionValue]

				switch (statement.operation) {

				case TokenType.AddAssign:
					Emit (node, ByteCode.CallFunc, TokenType.Add.ToString ());
					break;
				case TokenType.MinusAssign:
					Emit (node, ByteCode.CallFunc, TokenType.Minus.ToString ());
					break;
				case TokenType.MultiplyAssign:
					Emit (node, ByteCode.CallFunc, TokenType.Multiply.ToString ());
					break;
				case TokenType.DivideAssign:
					Emit (node, ByteCode.CallFunc, TokenType.Divide.ToString ());
					break;
				default:
					throw new ArgumentOutOfRangeException ();
				}

				// Stack now contains [destinationValue]
			}

			// Store the top of the stack in the variable
			Emit(node, ByteCode.StoreVariable, statement.destinationVariableName);

			// Clean up the stack
			Emit (node, ByteCode.Pop);

		}

		void GenerateCode(Node node, Parser.Expression expression) {

			// Expressions are either plain values, or function calls
			switch (expression.type) {
			case Parser.Expression.Type.Value:
				// Plain value? Emit that
				GenerateCode (node, expression.value);
				break;
			case Parser.Expression.Type.FunctionCall:
				// Evaluate all parameter expressions (which will
				// push them to the stack)
				foreach (var parameter in expression.parameters) {
					GenerateCode (node, parameter);
				}
				// And then call the function
				Emit (node, ByteCode.CallFunc, expression.function.name);
				break;
			}
		}

		void GenerateCode(Node node, Parser.ValueNode value) {

			// Push a value onto the stack

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
				Emit (node, ByteCode.PushVariable, value.value.variableName);
				break;
			case Value.Type.Null:
				Emit (node, ByteCode.PushNull);
				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}
		}




	}
}

