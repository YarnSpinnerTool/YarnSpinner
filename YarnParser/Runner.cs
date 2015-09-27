using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn
{
	

	public interface Continuity {
		void SetNumber(float number, string variableName);
		float GetNumber(string variableName);
	}

	public class Runner
	{



		public List<Parser.OptionStatement> currentOptions;

		private Implementation implementation;

		public Runner(Implementation implementation) {
			this.implementation = implementation;
		}

		// executes a node, and returns either the name of the next node to run
		// or null (indicating the dialogue is over)
		public string RunNode(Yarn.Parser.Node node)
		{

			currentOptions = new List<Parser.OptionStatement> ();
			RunStatements (node.statements);

			// If we have no options, all done
			if (currentOptions.Count == 0) {
				return null;
			} else {

				// We have options!

				// If we have precisely one option and it's got no
				// label, jump to it
				if (currentOptions.Count == 1 &&
					currentOptions[0].label == null) {
					return currentOptions [0].destination;
				}

				// Otherwise, ask which option to pick

				var optionStrings = new List<string> ();
				foreach (var option in currentOptions) {
					var label = option.label ?? option.destination;
					optionStrings.Add (label);
				}
				var selectedOptionNumber = implementation.RunOptions (optionStrings.ToArray ());

				// And jump to it!
				var selectedOption = currentOptions [selectedOptionNumber];


				return selectedOption.destination;
			}

		}

		void RunStatements(IEnumerable<Parser.Statement> statements) {
			if (statements == null) {
				return;
			}

			foreach (var statement in statements) {
				RunStatement (statement);
			}
		}

		void RunStatement (Parser.Statement statement) {


			switch (statement.type) {

			case Parser.Statement.Type.Block:
				RunStatements (statement.block.statements);
				break;

			case Parser.Statement.Type.Line:
				implementation.RunLine (statement.line);
				break;

			case Parser.Statement.Type.IfStatement:
				var condition = EvaluateExpression (statement.ifStatement.primaryClause.expression);
				if (condition != 0.0f) {
					RunStatements (statement.ifStatement.primaryClause.statements);
				} else {

					bool didRunElseIf = false;
					foreach (var elseIf in statement.ifStatement.elseIfClauses) {
						if (EvaluateExpression(elseIf.expression) != 0.0f) {
							RunStatements(elseIf.statements);
							didRunElseIf = true;
							break;
						}
					}

					if (didRunElseIf == false) {
						RunStatements (statement.ifStatement.elseClause.statements);
					}
				}
				break;


			case Parser.Statement.Type.OptionStatement:
				currentOptions.Add (statement.optionStatement);
				break;

			case Parser.Statement.Type.AssignmentStatement:
				RunAssignmentStatement (statement.assignmentStatement);
				break;

			case Parser.Statement.Type.ShortcutOptionGroup:

				RunShortcutOptionGroup (statement.shortcutOptionGroup);
				break;
			case Parser.Statement.Type.CustomCommand:
				RunCustomCommand (statement.customCommand);
				break;

			default:
				throw new NotImplementedException ("Unimplemented statement type " + statement.type);
			}
		

		}

		float EvaluateExpression(Parser.Expression expression) {
			
			switch (expression.type) {
			case Parser.Expression.Type.Value:
				return EvaluateValue (expression.value);
			case Parser.Expression.Type.Compound:

				var leftHand = EvaluateExpression (expression.leftHand);

				var operatorType = expression.operation.operatorType;
				var rightHand = EvaluateExpression (expression.rightHand);

				switch (operatorType) {
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
					return (leftHand!=0 && rightHand!=0) ? 1.0f : 0.0f;
				case TokenType.Or:
					return (leftHand!=0 || rightHand!=0) ? 1.0f : 0.0f;
				case TokenType.Xor:
					return (leftHand!=0 ^ rightHand!=0) ? 1.0f : 0.0f;				
				}

				throw new NotImplementedException ("Operator " + operatorType.ToString() + " is not yet implemented");
			}


			return 0.0f;


		}

		// TODO: this needs to be removed when proper expression parsing is done
		float EvaluateExpression(Parser.Value value) {
			return EvaluateValue(value);
		}

		float EvaluateValue(Parser.Value value) {
			switch (value.type) {
			case Parser.Value.Type.Number:
				return value.number;
			case Parser.Value.Type.Variable:
				return implementation.continuity.GetNumber (value.variableName);
			}
			return 0.0f;
		}

		void RunAssignmentStatement(Parser.AssignmentStatement assignment) {
			var variableName = assignment.destinationVariableName;

			var computedValue = EvaluateExpression (assignment.valueExpression);

			float originalValue = implementation.continuity.GetNumber (variableName);

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

		void RunShortcutOptionGroup (Parser.ShortcutOptionGroup shortcutOptionGroup)
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

			if (shortcutOptionGroup.epilogue != null)
				RunStatements(shortcutOptionGroup.epilogue.statements);

		}

		void RunCustomCommand (Parser.CustomCommand customCommand)
		{
			implementation.RunCommand (customCommand.command);
		}
	}
}