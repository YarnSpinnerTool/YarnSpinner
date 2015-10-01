using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn
{
	
	// Where we turn to for storing and loading variable data.
	public interface Continuity : IEnumerable {
		void SetNumber(string variableName, float number);
		float GetNumber(string variableName);
		void Clear();
	}


	internal class Runner
	{
		// The list of options that this node has currently developed.
		private List<Parser.OptionStatement> currentOptions;

		// The object that we send our lines, options and commands to for display and input.
		private Dialogue dialogue;

		internal Runner(Dialogue dialogue) {
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
				// Lines get forwarded to the implementation for display
				yield return new Dialogue.LineResult(statement.line);
				break;

			case Parser.Statement.Type.IfStatement:

				// Evaluate each clause in the statement, and run its statements if appropriate
				foreach (var clause in statement.ifStatement.clauses) {
					// if this clause's expression doesn't evaluate to 0, run it; alternatively,
					// if this clause has no expression (ie it's the 'else' clause) then also run it
					if (clause.expression == null || EvaluateExpression(clause.expression) != 0.0f) {
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
				// Deal with a custom command
				yield return new Dialogue.CommandResult (statement.customCommand.command);
				break;

			default:
				// Just in case we added a new type of statement and didn't implement it here
				throw new NotImplementedException ("YarnRunner: Unimplemented statement type " + statement.type);
			}
		

		}

		private float EvaluateExpression(Parser.Expression expression) {
			
			switch (expression.type) {
			case Parser.Expression.Type.Value:
				// just a regular value? return it
				return EvaluateValue (expression.value);

			case Parser.Expression.Type.Compound:

				// Recursively evaluate the left and right hand expressions
				var leftHand = EvaluateExpression (expression.leftHand);

				var rightHand = EvaluateExpression (expression.rightHand);

				// And then Do A Thing with the results:
				switch (expression.operation.operatorType) {
				case TokenType.Add:
					return leftHand + rightHand;
				case TokenType.Minus:
					return leftHand - rightHand;
				case TokenType.Multiply:
					return leftHand * rightHand;
				case TokenType.Divide:
					return leftHand / rightHand;

				case TokenType.GreaterThan:
					return leftHand > rightHand ? 1.0f : 0.0f;
				case TokenType.LessThan:
					return leftHand < rightHand ? 1.0f : 0.0f;
				case TokenType.GreaterThanOrEqualTo:
					return leftHand >= rightHand ? 1.0f : 0.0f;
				case TokenType.LessThanOrEqualTo:
					return leftHand <= rightHand ? 1.0f : 0.0f;

				case TokenType.EqualToOrAssign:					
				case TokenType.EqualTo:
					return leftHand == rightHand ? 1.0f : 0.0f;
				case TokenType.NotEqualTo:
					return leftHand != rightHand ? 1.0f : 0.0f;

				case TokenType.And:
					return (leftHand != 0 && rightHand != 0) ? 1.0f : 0.0f;
				case TokenType.Or:
					return (leftHand != 0 || rightHand != 0) ? 1.0f : 0.0f;
				case TokenType.Xor:
					return (leftHand != 0 ^ rightHand != 0) ? 1.0f : 0.0f;				
				}

				// whoa no
				throw new NotImplementedException ("Operator " + expression.operation.operatorType.ToString () 
					+ " is not yet implemented");
			}

			throw new NotImplementedException ("Unimplemented expression type " + expression.type.ToString ());

		}

		// Returns the actual value of this Value object.
		private float EvaluateValue(Parser.Value value) {
			switch (value.type) {
			case Parser.Value.Type.Number:
				return value.number;
			case Parser.Value.Type.Variable:
				dialogue.LogDebugMessage ("Checking value " + value.variableName);
				return dialogue.continuity.GetNumber (value.variableName);
			}
			return 0.0f;
		}

		// Assigns a value to a variable.
		private void RunAssignmentStatement(Parser.AssignmentStatement assignment) {

			// The place where we're stickin' this value.
			var variableName = assignment.destinationVariableName;

			// The value that's going into this variable.
			var computedValue = EvaluateExpression (assignment.valueExpression);

			// The current value of this variable.
			float originalValue = dialogue.continuity.GetNumber (variableName);

			// What shall we do with it?
			float finalValue = 0.0f;
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
			dialogue.continuity.SetNumber (variableName, finalValue);
		}

		private IEnumerable<Dialogue.RunnerResult> RunShortcutOptionGroup (Parser.ShortcutOptionGroup shortcutOptionGroup)
		{
			var optionsToDisplay = new List<Parser.ShortcutOption> ();

			// Determine which options to present
			foreach (var option in shortcutOptionGroup.options) {
				var include = true;
				if (option.condition != null) {
					include = EvaluateExpression(option.condition) != 0.0f;
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
					dialogue.LogErrorMessage ("Option chooser was never called!");
					yield break;
				}

				if (selectedOption.optionNode != null) {
					foreach (var command in RunStatements(selectedOption.optionNode.statements)) {
						yield return command;
					}
				}
					
			}

			// Done running these options; run the stuff that came afterwards
			if (shortcutOptionGroup.epilogue != null) {
				foreach (var command in RunStatements(shortcutOptionGroup.epilogue.statements)) {
					yield return command;
				}
			}
				

		}
	}

	// Very simple continuity class that keeps all variables in memory
	public class InMemoryContinuity : Yarn.Continuity {
		public IEnumerator GetEnumerator ()
		{
			return variables.GetEnumerator ();
		}

		Dictionary<string, float> variables = new Dictionary<string, float>();

		public void SetNumber (string variableName, float number)
		{
			variables [variableName] = number;
		}

		public float GetNumber (string variableName)
		{
			float value = 0.0f;
			if (variables.ContainsKey(variableName)) {

				value = variables [variableName];

			}
			return value;
		}				

		public void Clear() {
			variables.Clear ();
		}
	}
}