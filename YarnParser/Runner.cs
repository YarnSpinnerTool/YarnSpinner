using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn
{
	
	// Where we turn to for storing and loading variable data.
	public interface Continuity {
		void SetNumber(float number, string variableName);
		float GetNumber(string variableName);
	}

	public class Runner
	{


		// The list of options that this node has currently developed.
		private List<Parser.OptionStatement> currentOptions;

		// The object that we send our lines, options and commands to for display and input.
		private Implementation implementation;

		public Runner(Implementation implementation) {
			this.implementation = implementation;
		}

		// executes a node, and returns either the name of the next node to run
		// or null (indicating the dialogue is over)
		public string RunNode(Yarn.Parser.Node node)
		{

			// Clear the list of options when we start a new node
			currentOptions = new List<Parser.OptionStatement> ();

			// Run all of the statements in this node
			RunStatements (node.statements);

			// If we have no options, we're all done
			if (currentOptions.Count == 0) {
				return null;
			} else {
				// We have options!

				// If we have precisely one option and it's got no label, jump to it
				if (currentOptions.Count == 1 &&
					currentOptions[0].label == null) {
					return currentOptions [0].destination;
				}

				// Otherwise, ask which option to pick...
				var optionStrings = new List<string> ();
				foreach (var option in currentOptions) {
					var label = option.label ?? option.destination;
					optionStrings.Add (label);
				}
				var selectedOptionNumber = implementation.RunOptions (optionStrings.ToArray ());

				// And jump to its destination!
				var selectedOption = currentOptions [selectedOptionNumber];
				return selectedOption.destination;
			}

		}

		// Run a list of statements.
		private void RunStatements(IEnumerable<Parser.Statement> statements) {
			
			if (statements == null) {
				return;
			}

			foreach (var statement in statements) {
				RunStatement (statement);
			}
		}

		// Run a single statement.
		private void RunStatement (Parser.Statement statement) {


			switch (statement.type) {

			case Parser.Statement.Type.Block:
				// Blocks just contain statements, so run them!
				RunStatements (statement.block.statements);
				break;

			case Parser.Statement.Type.Line:
				// Lines get forwarded to the implementation for display
				implementation.RunLine (statement.line);
				break;

			case Parser.Statement.Type.IfStatement:

				// Evaluate each clause in the statement, and run its statements if appropriate
				foreach (var clause in statement.ifStatement.clauses) {
					// if this clause's expression doesn't evaluate to 0, run it; alternatively,
					// if this clause has no expression (ie it's the 'else' clause) then also run it
					if (clause.expression == null || EvaluateExpression(clause.expression) != 0.0f) {
						RunStatements (clause.statements);
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
				RunShortcutOptionGroup (statement.shortcutOptionGroup);
				break;
			case Parser.Statement.Type.CustomCommand:
				// Deal with a custom command
				RunCustomCommand (statement.customCommand);
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
				return implementation.continuity.GetNumber (value.variableName);
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
			float originalValue = implementation.continuity.GetNumber (variableName);

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

			implementation.continuity.SetNumber (finalValue, variableName);
		}

		private void RunShortcutOptionGroup (Parser.ShortcutOptionGroup shortcutOptionGroup)
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

				var selectedIndex = implementation.RunOptions(optionStrings.ToArray());
				var selectedOption = optionsToDisplay[selectedIndex];

				if (selectedOption.optionNode != null)
					RunStatements(selectedOption.optionNode.statements);
			}

			// Done running these options; run the stuff that came afterwards
			if (shortcutOptionGroup.epilogue != null)
				RunStatements(shortcutOptionGroup.epilogue.statements);

		}

		// Custom commands are just forwarded to the implementation
		private void RunCustomCommand (Parser.CustomCommand customCommand)
		{
			implementation.RunCommand (customCommand.command);
		}
	}
}