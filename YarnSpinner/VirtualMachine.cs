using System;
using System.Collections.Generic;

namespace Yarn
{
	internal class VirtualMachine
	{

		public delegate void LineHandler(Dialogue.LineResult line);
		public delegate void OptionsHandler(Dialogue.OptionSetResult options);
		public delegate void CommandHandler(Dialogue.CommandResult command);
		public delegate void CompleteHandler();

		internal VirtualMachine (Dialogue d, Program p)
		{
			program = p;
			dialogue = d;
			state = new State ();
		}

		void Reset() {
			state = new State();
		}

		public LineHandler lineHandler;
		public OptionsHandler optionsHandler;
		public CommandHandler commandHandler;
		public CompleteHandler completeHandler;

		private Dialogue dialogue;

		private Program program;
		private State state;

		IList<Instruction> currentNodeInstructions;

		internal void SetNode(string nodeName) {
			if (program.nodes.ContainsKey(nodeName) == false) {
				throw new ArgumentException ("No node named " + nodeName);
			}

			currentNodeInstructions = program.nodes [nodeName].instructions;
			state.currentNode = nodeName;
			state.programCounter = 0;
			state.stack.Clear ();
		}

		internal void RunNext() {



			Instruction currentInstruction = currentNodeInstructions [state.programCounter];

			RunInstruction (currentInstruction);

			if (state.programCounter >= currentNodeInstructions.Count) {
				// end of line
			}

		}

		internal int FindInstructionPointForLabel(string labelName) {
			int i = 0;

			foreach (Instruction instruction in currentNodeInstructions) {
				if (instruction.operation == ByteCode.Label && 
					(string)instruction.operandA == labelName) {
					return i;
				}
				i++;
					
			}

			throw new IndexOutOfRangeException ("Unknown label " + labelName);
		}



		internal void RunInstruction(Instruction i) {
			switch (i.operation) {
			case ByteCode.Label:
				// no-op
				break;
			case ByteCode.Jump:

				state.programCounter = FindInstructionPointForLabel ((string)i.operandA);

				break;
			case ByteCode.RunLine:

				var lineText = program.GetString ((int)i.operandA);

				lineHandler(new Dialogue.LineResult (lineText));
				
				break;
			case ByteCode.RunCommand:

				commandHandler(
					new Dialogue.CommandResult((string)i.operandA)
				);

				break;
			case ByteCode.PushString:

				state.PushValue (program.GetString((int) i.operandA));

				break;
			case ByteCode.PushNumber:

				state.PushValue (i.operandA);

				break;
			case ByteCode.PushBool:

				state.PushValue (i.operandA);

				break;
			case ByteCode.JumpIfTrue:

				if (state.PopValue().AsBool == true) {
					state.programCounter = FindInstructionPointForLabel((string)i.operandA);
				}
				break;
			case ByteCode.Pop:

				state.PopValue();
				break;
			case ByteCode.CallFunc:

				var functionName = (string)i.operandA;
				var function = dialogue.library.GetFunction (functionName);
				{
					Value[] parameters = new Value[function.paramCount];
					for (int param = function.paramCount; param  > 0; param --) {
						parameters [param] = state.PopValue ();
					}
					var result = function.InvokeWithArray(parameters);

					if (function.returnsValue) {
						state.PushValue (result);
					}
				}

				break;
			case ByteCode.Load:

				var variableName = (string)i.operandA;
				var loadedValue = dialogue.continuity.GetNumber (variableName);
				state.PushValue (loadedValue);

				break;
			case ByteCode.Store:

				var topValue = state.PopValue ();
				var destinationVariableName = (string)i.operandA;

				if (topValue.type != Value.Type.Number) {
					dialogue.continuity.SetNumber (destinationVariableName, topValue.AsNumber);
				} else {
					throw new NotImplementedException ("Only numbers can be stored in variables.");
				}

				break;
			case ByteCode.Stop:

				completeHandler ();

				break;
			case ByteCode.RunNode:

				var nodeName = (string)i.operandA;

				SetNode (nodeName);

				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}
		}

		
	}
}

