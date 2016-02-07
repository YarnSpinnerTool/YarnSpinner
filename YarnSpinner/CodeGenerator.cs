using System;
using System.Collections.Generic;

namespace Yarn
{
	internal class State {
		CompiledYarnProgram program;

		int programCounter = 0;

		Stack<Value> stack = new Stack<Value>();
	}

	internal class CompiledYarnProgram {
		public string[] strings;

		public Instruction[] instructions;
	}

	internal struct Instruction {
		internal ByteCode operation;
		internal object operandA;
		internal object operandB;
	}

	internal enum ByteCode {
		Label,			// opA = string: label name
		Jump,			// opA = string: label name
		DefineString,	// opA = string: text
		RunLine,		// opA = int: string number
		RunCommand,		// opA = string: command text
	}

	internal class CodeGenerator
	{

		CompiledYarnProgram program;

		List<string> registeredStrings;

		CodeGenerator ()
		{
		}

		CompiledYarnProgram GenerateCode(Parser.Node node) {

			program =  new CompiledYarnProgram();
			registeredStrings = new List<string> ();

			var instructions = new List<Instruction> ();

			foreach (var statement in node.statements) {
				var statementInstructions = GenerateCode (statement);
				instructions.AddRange (statementInstructions);
			}

			program.instructions = instructions.ToArray ();



			return program;
		}

		int RegisterString(string theString) {
			var index = registeredStrings.IndexOf (theString);
			if (index > 0)
				return index;

			// It's not in the list; append it
			registeredStrings.Add(theString);
			return registeredStrings.Count - 1;
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
				return GenerateCode (statement.customCommand);

			default:
				throw new ArgumentOutOfRangeException ();
			}


		}

		IEnumerable<Instruction> GenerateCode(Parser.CustomCommand statement) {

			var commandString = RegisterString(statement.clientCommand);

			Instruction i = new Instruction();

			i.operation = ByteCode.RunCommand;
			i.operandA = commandString;

			return new Instruction[] { i };
		}

		IEnumerable<Instruction> GenerateCode(Parser.ShortcutOptionGroup statement) {
		}

		IEnumerable<Instruction> GenerateCode(Parser.IfStatement statement) {
		}

		IEnumerable<Instruction> GenerateCode(Parser.OptionStatement statement) {
		}

		IEnumerable<Instruction> GenerateCode(Parser.AssignmentStatement statement) {
		}




	}
}

