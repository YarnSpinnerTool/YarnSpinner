using System;
using System.Collections.Generic;

namespace Yarn
{
	internal class VirtualMachine
	{

		public delegate void LineHandler(Dialogue.LineResult line);
		public delegate void OptionsHandler(Dialogue.OptionSetResult options);
		public delegate void CommandHandler(Dialogue.CommandResult command);
		public delegate void NodeCompleteHandler(Dialogue.NodeCompleteResult complete);

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
		public NodeCompleteHandler nodeCompleteHandler;

		private Dialogue dialogue;

		private Program program;
		private State state;

		public string currentNode { get { return state.currentNode; } }

		// List of options, where each option = <string id, destination node>
		private List<KeyValuePair<int,string>> currentOptions = new List<KeyValuePair<int, string>>();

		public enum ExecutionState {
			Stopped,
			WaitingOnOptionSelection,
			Running
		}

		public ExecutionState executionState { get; private set; }

		IList<Instruction> currentNodeInstructions;

		public void SetNode(string nodeName) {
			if (program.nodes.ContainsKey(nodeName) == false) {
				dialogue.LogErrorMessage("No node named " + nodeName);
				executionState = ExecutionState.Stopped;
				return;
			}

			dialogue.LogDebugMessage ("Running node " + nodeName);

			currentNodeInstructions = program.nodes [nodeName].instructions;
			state.currentNode = nodeName;
			state.programCounter = 0;
			state.stack.Clear ();
		}

		public void Stop() {
			executionState = ExecutionState.Stopped;
		}

		internal void RunNext() {

			if (executionState == ExecutionState.WaitingOnOptionSelection) {
				dialogue.LogErrorMessage ("Cannot continue running dialogue. Still waiting on option selection.");
				return;
			}

			if (executionState == ExecutionState.Stopped)
				executionState = ExecutionState.Running;

			Instruction currentInstruction = currentNodeInstructions [state.programCounter];

			RunInstruction (currentInstruction);

			state.programCounter++;

			if (state.programCounter >= currentNodeInstructions.Count) {
				executionState = ExecutionState.Stopped;
				nodeCompleteHandler (new Dialogue.NodeCompleteResult (null));
				dialogue.LogDebugMessage ("Run complete.");
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
			case ByteCode.JumpTo:

				state.programCounter = FindInstructionPointForLabel ((string)i.operandA);

				break;
			case ByteCode.RunLine:

				var lineText = program.GetString ((int)i.operandA);

				lineHandler (new Dialogue.LineResult (lineText));
				
				break;
			case ByteCode.RunCommand:

				commandHandler (
					new Dialogue.CommandResult ((string)i.operandA)
				);

				break;
			case ByteCode.PushString:

				state.PushValue (program.GetString ((int)i.operandA));

				break;
			case ByteCode.PushNumber:

				state.PushValue (i.operandA);

				break;
			case ByteCode.PushBool:

				state.PushValue (i.operandA);

				break;
			case ByteCode.PushNull:

				state.PushValue (new Value ());

				break;
			case ByteCode.JumpIfFalse:

				if (state.PeekValue ().AsBool == false) {
					state.programCounter = FindInstructionPointForLabel ((string)i.operandA);
				}
				break;
			
			case ByteCode.Jump:

				var jumpDestination = state.PeekValue ().AsString;
				state.programCounter = FindInstructionPointForLabel (jumpDestination);

				break;
			
			case ByteCode.Pop:

				state.PopValue ();
				break;
			case ByteCode.CallFunc:

				var functionName = (string)i.operandA;
				var function = dialogue.library.GetFunction (functionName);
				{
					// Get the parameters, which were pushed in reverse
					Value[] parameters = new Value[function.paramCount];
					for (int param = function.paramCount - 1; param >= 0; param--) {
						parameters [param] = state.PopValue ();
					}

					// Invoke the function
					var result = function.InvokeWithArray (parameters);

					// If the function returns a value, push it
					if (function.returnsValue) {
						state.PushValue (result);
					}
				}

				break;
			case ByteCode.PushVariable:

				var variableName = (string)i.operandA;
				var loadedValue = dialogue.continuity.GetNumber (variableName);
				state.PushValue (loadedValue);

				break;
			case ByteCode.StoreVariable:

				var topValue = state.PeekValue ();
				var destinationVariableName = (string)i.operandA;

				if (topValue.type == Value.Type.Number) {
					dialogue.continuity.SetNumber (destinationVariableName, topValue.AsNumber);
				} else {
					throw new NotImplementedException ("Only numbers can be stored in variables.");
				}

				break;
			case ByteCode.Stop:

				executionState = ExecutionState.Stopped;

				nodeCompleteHandler (new Dialogue.NodeCompleteResult (null));

				break;
			case ByteCode.RunNode:

				

				var nodeName = state.PopValue ().AsString;

				SetNode (nodeName);

				break;
			case ByteCode.AddOption:


				currentOptions.Add (new KeyValuePair<int, string> ((int)i.operandA, (string)i.operandB));


				break;
			case ByteCode.ShowOptions:

				var optionStrings = new List<string> ();
				foreach (var option in currentOptions) {
					optionStrings.Add (program.GetString (option.Key));
				}

				executionState = ExecutionState.WaitingOnOptionSelection;

				optionsHandler (new Dialogue.OptionSetResult (optionStrings, delegate (int selectedOption) {
					var destinationNode = currentOptions[selectedOption].Value;
					state.PushValue(destinationNode);
					executionState = ExecutionState.Running;
					currentOptions.Clear();
				}));

				break;
			default:
				executionState = ExecutionState.Stopped;
				throw new ArgumentOutOfRangeException ();
			}
		}

		
	}
}

