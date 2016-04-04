using System;
using System.Collections.Generic;

namespace Yarn
{
	

	internal class Program {

		public Dictionary<string,string> strings = new Dictionary<string, string> ();

		public Dictionary<string, Node> nodes = new Dictionary<string, Node> ();

		private int stringCount = 0;

		public string RegisterString(string theString, string forNode) {

			var key = string.Format ("{0}-{1}", forNode, stringCount++);

			// It's not in the list; append it
			strings.Add(key, theString);

			return key;
		}

		public string GetString(string key) {
			string value = null;
			strings.TryGetValue (key, out value);
			return value;
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

				/* sb.AppendLine ();
				sb.AppendLine ("Label table:");

				foreach (var label in entry.Value.labels) {
					sb.AppendLine (string.Format ("{0,12} : {1}", label.Key, label.Value));
				}*/

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

		public string GetTextForNode(string nodeName) {
			return this.GetString (nodes [nodeName].sourceTextStringID);
		}

		public void Include (Program otherProgram)
		{
			foreach (var otherNodeName in otherProgram.nodes) {

				if (nodes.ContainsKey(otherNodeName.Key)) {
					throw new InvalidOperationException (string.Format ("This program already contains a node named {0}", otherNodeName.Key));
				}

				nodes [otherNodeName.Key] = otherNodeName.Value;
			}

			foreach (var otherString in otherProgram.strings) {

				if (nodes.ContainsKey(otherString.Key)) {
					throw new InvalidOperationException (string.Format ("This program already contains a string with key {0}", otherString.Key));
				}

				strings [otherString.Key] = otherString.Value;
			}
		}
	}

	internal class Node {

		public List<Instruction> instructions = new List<Instruction>();

		public string name;

		// the entry in the program's string table that contains
		// the original text of this node; -1 if this is not available
		public string sourceTextStringID = null;

		public Dictionary<string, int> labels = new Dictionary<string, int>();
	}

	internal struct Instruction {
		internal ByteCode operation;
		internal object operandA;
		internal object operandB;

		public  string ToString(Program p, Library l) {

			// Labels are easy: just dump out the name
			if (operation == ByteCode.Label) {
				return operandA + ":";
			}

			// Convert the operands to strings
			var opAString = operandA != null ? operandA.ToString () : "";
			var opBString = operandB != null ? operandB.ToString () : "";

			// Generate a comment, if the instruction warrants it
			string comment = "";

			// Stack manipulation comments
			var pops = 0;
			var pushes = 0;

			switch (operation) {

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
				var function = l.GetFunction ((string)operandA);

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
				comment += string.Format ("Pops {0}, Pushes {1}", pops, pushes);
			else if (pops > 0)
				comment += string.Format ("Pops {0}", pops);
			else if (pushes > 0)
				comment += string.Format ("Pushes {0}", pushes);

			// String lookup comments
			switch (operation) {
			case ByteCode.PushString:
			case ByteCode.RunLine:
			case ByteCode.AddOption:

				// Add the string for this option, if it has one
				if ((string)operandA != null) {
					var text = p.GetString((string)operandA);
					comment += string.Format ("\"{0}\"", text);
				}

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



	internal class Compiler
	{
		

		struct CompileFlags {
			// should we emit code that turns (VAR_SHUFFLE_OPTIONS) off
			// after the next RunOptions bytecode?
			public bool DisableShuffleOptionsAfterNextSet;
		}

		CompileFlags flags;

		internal Program program { get; private set; }

		internal Compiler (string programName)
		{
			program = new Program ();
		}

		internal void CompileNode(Parser.Node node) {

			if (program.nodes.ContainsKey(node.name)) {
				throw new ArgumentException ("Duplicate node name " + node.name);
			}

			var compiledNode =  new Node();

			compiledNode.name = node.name;

			var startLabel = RegisterLabel ();
			Emit (compiledNode, ByteCode.Label, startLabel);

			foreach (var statement in node.statements) {
				GenerateCode (compiledNode, statement);
			}

			// Does this node end after emitting AddOptions codes
			// without calling ShowOptions?

			// Note: this only works when we know that we don't have
			// AddOptions and then Jump up back into the code to run them.
			// TODO: A better solution would be for the parser to flag
			// whether a node has Options at the end.
			var hasRemainingOptions = false;
			foreach (var instruction in compiledNode.instructions) {
				if (instruction.operation == ByteCode.AddOption) {
					hasRemainingOptions = true;
				}
				if (instruction.operation == ByteCode.ShowOptions) {
					hasRemainingOptions = false;
				}
			}

			// If this compiled node has no lingering options to show at the end of the node, then stop at the end
			if (hasRemainingOptions == false) {
				Emit (compiledNode, ByteCode.Stop);
			} else {
				// Otherwise, show the accumulated nodes and then jump to the selected node

				Emit (compiledNode, ByteCode.ShowOptions);

				if (flags.DisableShuffleOptionsAfterNextSet == true) {
					Emit (compiledNode, ByteCode.PushBool, false);
					Emit (compiledNode, ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
					Emit (compiledNode, ByteCode.Pop);
					flags.DisableShuffleOptionsAfterNextSet = false;
				}

				Emit (compiledNode, ByteCode.RunNode);
			}

			if (node.source != null) {
				compiledNode.sourceTextStringID = program.RegisterString (node.source, node.name);
			}

			program.nodes [compiledNode.name] = compiledNode;
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

			if (code == ByteCode.Label) {
				// Add this label to the label table
				node.labels.Add ((string)instruction.operandA, node.instructions.Count - 1);
			}
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

			// If this command is an evaluable expression, evaluate it
			if (statement.expression != null) {
				GenerateCode (node, statement.expression);
			} else {
				switch (statement.clientCommand) {
				case "stop":
					Emit (node, ByteCode.Stop);
					break;
				case "shuffleNextOptions":
					// Emit code that sets "VAR_SHUFFLE_OPTIONS" to true
					Emit (node, ByteCode.PushBool, true);
					Emit (node, ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
					Emit (node, ByteCode.Pop);
					flags.DisableShuffleOptionsAfterNextSet = true;
					break;

				default:
					Emit (node, ByteCode.RunCommand, statement.clientCommand);
					break;
				}
			}

		}

		void GenerateCode(Node node, string line) {
			var num = program.RegisterString (line, node.name);

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

				var labelStringID = program.RegisterString (shortcutOption.label, node.name);

				Emit (node, ByteCode.AddOption, labelStringID, optionDestinationLabel);

				if (shortcutOption.condition != null) {
					Emit (node, ByteCode.Label, endOfClauseLabel);
					Emit (node, ByteCode.Pop);
				}

				optionCount++;
			}

			Emit (node, ByteCode.ShowOptions);

			if (flags.DisableShuffleOptionsAfterNextSet == true) {
				Emit (node, ByteCode.PushBool, false);
				Emit (node, ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
				Emit (node, ByteCode.Pop);
				flags.DisableShuffleOptionsAfterNextSet = false;
			}

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

			var destination = statement.destination;

			if (statement.label == null) {
				// this is a jump to another node
				Emit(node, ByteCode.RunNode, destination); 
			} else {
				var stringID = program.RegisterString (statement.label, node.name);

				Emit (node, ByteCode.AddOption, stringID, destination);
			}

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
				// If this function has a variable number of parameters, put
				// the number of parameters that were passed onto the stack
				if (expression.function.paramCount == -1) {
					Emit (node, ByteCode.PushNumber, expression.parameters.Count);
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
				var id = program.RegisterString (value.value.stringValue, node.name);
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

