using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn
{
	public class Runner
	{

		public interface Continuity {
			void SetNumber(float number, string variableName);
			float GetNumber(string variableName);
		}
			
		public delegate void RunLineCallback(string lineText);
		public delegate int RunOptionsCallback(string[] options);
		public delegate void NodeCompleteCallback(string nextNodeName);

		// This will be repeatedly called when we encounter
		public RunLineCallback RunLine;

		public RunOptionsCallback RunOptions;

		public NodeCompleteCallback NodeComplete;

		public List<Parser.OptionStatement> currentOptions;

		public Continuity continuity;
		
		public void RunNode(Yarn.Parser.Node node)
		{

			currentOptions = new List<Parser.OptionStatement> ();
			RunStatements (node.statements);

			// If we have no options, all done
			if (currentOptions.Count == 0) {
				NodeComplete (null);
				return;
			} else {

				// We have options!

				// If we have precisely one option and it's got no
				// label, jump to it
				if (currentOptions.Count == 1 &&
					currentOptions[0].label == null) {
					NodeComplete (currentOptions [0].destination);
					return;
				}

				// Otherwise, ask which option to pick

				var optionStrings = new List<string> ();
				foreach (var option in currentOptions) {
					var label = option.label ?? option.destination;
					optionStrings.Add (label);
				}
				var selectedOptionNumber = RunOptions (optionStrings.ToArray ());

				// And jump to it!
				var selectedOption = currentOptions [selectedOptionNumber];

				NodeComplete (selectedOption.destination);
				return;
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
				RunLine (statement.line);
				break;

			case Parser.Statement.Type.IfStatement:
				var condition = EvaluateExpression (statement.ifStatement.expression);
				if (condition != 0.0f) {
					RunStatements (statement.ifStatement.statements);
				} else {
					RunStatements (statement.ifStatement.elseStatements);
				}
				break;


			case Parser.Statement.Type.OptionStatement:
				currentOptions.Add (statement.optionStatement);
				break;

			case Parser.Statement.Type.AssignmentStatement:
				RunAssignmentStatement (statement.assignmentStatement);
				break;
			}
		

		}

		// TODO: assignment (including operators like +=)
		// TODO: more operators

		float EvaluateExpression(Parser.Expression expression) {

			switch (expression.type) {
			case Parser.Expression.Type.PrimitiveValue:
				return EvaluateValue (expression.value);
			case Parser.Expression.Type.Expression:

				var leftHand = EvaluateExpression (expression.leftHand);

				var operatorType = expression.exprOperator.operatorType;
				var rightHand = EvaluateExpression (expression.rightHand);

				switch (operatorType) {
				case TokenType.Add:
					return leftHand + rightHand;
				case TokenType.Minus:
					return leftHand - rightHand;

				case TokenType.EqualToOrAssign:					
				case TokenType.EqualTo:
					return leftHand == rightHand ? 1.0f : 0.0f;
				case TokenType.NotEqualTo:
					return leftHand != rightHand ? 1.0f : 0.0f;
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
				return continuity.GetNumber (value.variableName);
			}
			return 0.0f;
		}

		void RunAssignmentStatement(Parser.AssignmentStatement assignment) {
			var variableName = assignment.destinationVariableName;

			var computedValue = EvaluateExpression (assignment.valueExpression);

			float originalValue = continuity.GetNumber (variableName);

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

			continuity.SetNumber (finalValue, variableName);
		}
	}
}