/*

The MIT License (MIT)

Copyright (c) 2015 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn
{
	


	internal class TreeRunner
	{
		// The list of options that this node has currently developed.
		private List<Parser.OptionStatement> currentOptions;

		// The object that we send our lines, options and commands to for display and input.
		private Dialogue dialogue;

		internal TreeRunner(Dialogue dialogue) {
			this.dialogue = dialogue;
		}

		// executes a node, and returns either the name of the next node to run
		// or null (indicating the dialogue is over)
		internal IEnumerable<Dialogue.RunnerResult> RunNode(Yarn.Parser.Node node)
		{

			// Clear the list of options when we start a new node
			currentOptions = new List<Parser.OptionStatement> ();

			// Run all of the statements in this node
			foreach (var command in RunStatements (node.statements)) {
				yield return command;
			}

			// If we have no options, we're all done
			if (currentOptions.Count == 0) {
				yield return new Dialogue.NodeCompleteResult (null);
				yield break;
			} else {
				// We have options!

				// If we have precisely one option and it's got no label, jump to it
				if (currentOptions.Count == 1 &&
					currentOptions[0].label == null) {
					yield return new Dialogue.NodeCompleteResult (currentOptions [0].destination);
					yield break;
				}

				// Otherwise, ask which option to pick...
				var optionStrings = new List<string> ();
				foreach (var option in currentOptions) {
					var label = option.label ?? option.destination;
					optionStrings.Add (label);
				}

				Parser.OptionStatement selectedOption = null;

				yield return new Dialogue.OptionSetResult (optionStrings, delegate(int selectedOptionIndex) {
					selectedOption = currentOptions[selectedOptionIndex];
				});

				if (selectedOption == null) {
					dialogue.LogErrorMessage ("Option chooser was never called!");
					yield break;
				}

				// And jump to its destination!
				yield return new Dialogue.NodeCompleteResult(selectedOption.destination);
			}
			yield break;

		}

		// Run a list of statements.
		private IEnumerable<Dialogue.RunnerResult> RunStatements(IEnumerable<Parser.Statement> statements) {
			
			if (statements == null) {
				yield break;
			}

			foreach (var statement in statements) {
				foreach (var command in RunStatement (statement)) {
					yield return command;
				}

			}
		}

		// Run a single statement.
		private IEnumerable<Dialogue.RunnerResult> RunStatement (Parser.Statement statement) {


			switch (statement.type) {

			case Parser.Statement.Type.Block:
				// Blocks just contain statements, so run them!
				foreach (var command in RunStatements (statement.block.statements)) {
					yield return command;
				}
				break;

			case Parser.Statement.Type.Line:
				// Lines get forwarded to the client for display
				yield return new Dialogue.LineResult(statement.line);
				break;

			case Parser.Statement.Type.IfStatement:

				// Evaluate each clause in the statement, and run its statements if appropriate
				foreach (var clause in statement.ifStatement.clauses) {
					// if this clause's expression doesn't evaluate to 0, run it; alternatively,
					// if this clause has no expression (ie it's the 'else' clause) then also run it
					if (clause.expression == null || EvaluateExpression(clause.expression).AsBool != false) {
						foreach (var command in  RunStatements (clause.statements)) {
							yield return command;
						}
						// don't continue on to the other clauses
						break;
					}
				}
				break;

			case Parser.Statement.Type.OptionStatement:
				// If we encounter an option, record it so that we can present it later
				currentOptions.Add (statement.optionStatement);
				break;

			case Parser.Statement.Type.AssignmentStatement:
				// Evaluate the expression and assign it to a variable
				RunAssignmentStatement (statement.assignmentStatement);
				break;

			case Parser.Statement.Type.ShortcutOptionGroup:
				// Evaluate and present the options, then run the stuff that came after the options
				foreach (var command in RunShortcutOptionGroup (statement.shortcutOptionGroup)) {
					yield return command;
				}
				break;

			case Parser.Statement.Type.CustomCommand:
				// Deal with a custom command - it's either an expression or a client command
				// If it's an expression, evaluate it
				// If it's a client command, yield it to the client

				switch (statement.customCommand.type) {
				case Parser.CustomCommand.Type.Expression:
					EvaluateExpression (statement.customCommand.expression);
					break;
				case Parser.CustomCommand.Type.ClientCommand:
					yield return new Dialogue.CommandResult (statement.customCommand.clientCommand);
					break;
				}

				break;

			default:
				// Just in case we added a new type of statement and didn't implement it here
				throw new NotImplementedException ("YarnRunner: Unimplemented statement type " + statement.type);
			}
		

		}

		private Yarn.Value EvaluateExpression(Parser.Expression expression) {

			if (expression == null)
				return Yarn.Value.NULL;
			
			switch (expression.type) {
			case Parser.Expression.Type.Value:
				// just a regular value? return it
				return EvaluateValue (expression.value.value);

			case Parser.Expression.Type.FunctionCall:
				// get the function
				var func = expression.function;

				// evaluate all parameters
				var evaluatedParameters = new List<Value> ();

				foreach (var param in expression.parameters) {
					var expr = EvaluateExpression (param);
					evaluatedParameters.Add (expr);
				}

				var result = func.InvokeWithArray (evaluatedParameters.ToArray ());

				return result;

			}

			throw new NotImplementedException ("Unimplemented expression type " + expression.type.ToString ());

		}

		// Returns the actual value of this Value object.
		private Yarn.Value EvaluateValue(Value value) {
			switch (value.type) {
			case Value.Type.Variable:
				dialogue.LogDebugMessage ("Checking value " + value.variableName);
				return dialogue.continuity.GetValue (value.variableName);
			default:
				return value;
			}
		}

		// Assigns a value to a variable.
		private void RunAssignmentStatement(Parser.AssignmentStatement assignment) {

			// The place where we're stickin' this value.
			var variableName = assignment.destinationVariableName;

			// The value that's going into this variable.
			var computedValue = EvaluateExpression (assignment.valueExpression);

			// The current value of this variable.
			Value originalValue = dialogue.continuity.GetValue (variableName);

			// What shall we do with it?

			Value finalValue = Value.NULL;
			switch (assignment.operation) {
			case TokenType.EqualToOrAssign:
				finalValue = computedValue;
				break;
			case TokenType.AddAssign:
				finalValue = originalValue + computedValue;
				break;
			case TokenType.MinusAssign:
				finalValue = originalValue - computedValue;
				break;
			case TokenType.MultiplyAssign:
				finalValue = originalValue * computedValue;
				break;
			case TokenType.DivideAssign:
				finalValue = originalValue / computedValue;
				break;
			}

			dialogue.LogDebugMessage(string.Format("Set {0} to {1}", variableName, finalValue));
			dialogue.continuity.SetValue (variableName, finalValue);
		}

		private IEnumerable<Dialogue.RunnerResult> RunShortcutOptionGroup (Parser.ShortcutOptionGroup shortcutOptionGroup)
		{
			var optionsToDisplay = new List<Parser.ShortcutOption> ();

			// Determine which options to present
			foreach (var option in shortcutOptionGroup.options) {
				var include = true;
				if (option.condition != null) {
					include = EvaluateExpression(option.condition).AsBool != false;
				}
				if (include) {
					optionsToDisplay.Add(option);
				}
			}

			if (optionsToDisplay.Count > 0) {
				// Give this list to our client
				var optionStrings = new List<string> ();

				foreach (var option in optionsToDisplay) {
					optionStrings.Add(option.label);
				}

				Parser.ShortcutOption selectedOption = null;

				yield return new Dialogue.OptionSetResult (optionStrings, delegate(int selectedOptionIndex) {
					selectedOption = optionsToDisplay[selectedOptionIndex];
				});

				if (selectedOption == null) {
					dialogue.LogErrorMessage ("The OptionChooser I provided was not called before the " +
						"next line was run! Stopping dialogue.");
					yield break;
				}

				if (selectedOption.optionNode != null) {
					foreach (var command in RunStatements(selectedOption.optionNode.statements)) {
						yield return command;
					}
				}
					
			}

		}
	}

	// Very simple continuity class that keeps all variables in memory
	public class MemoryVariableStore : Yarn.BaseVariableStorage {
		Dictionary<string, Value> variables = new Dictionary<string, Value>();

		public override void SetValue (string variableName, Value value)
		{
			variables [variableName] = value;
		}

		public override Value GetValue (string variableName)
		{
			Value value = Value.NULL;
			if (variables.ContainsKey(variableName)) {

				value = variables [variableName];

			}
			return value;
		}				

		public override void Clear() {
			variables.Clear ();
		}
	}
}