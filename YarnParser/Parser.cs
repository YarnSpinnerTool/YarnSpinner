using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Yarn {

	[Serializable]
	public class ParseException : Exception {
		public static ParseException Make(Token foundToken, TokenType expectedType) {

			string message = string.Format("{0}:{1}: Expected {2}, but found {3}",
			                               foundToken.lineNumber,
			                               foundToken.columnNumber,
			                               expectedType.ToString(),
			                               foundToken.type.ToString()
			                               );
			return new ParseException(message);
		}

		public static ParseException Make(Token foundToken, ICollection<TokenType> expectedTypes) {

			string possibleValues = string.Join(",", expectedTypes);

			string message = string.Format("{0}:{1}: Expected {2}, but found {3}",
			                               foundToken.lineNumber,
			                               foundToken.columnNumber,
			                               possibleValues,
			                               foundToken.type.ToString()
			                               );
			return new ParseException(message);
		}

		public ParseException ()
		{}
		
		public ParseException (string message) 
			: base(message)
		{}
		
		public ParseException (string message, Exception innerException)
			: base (message, innerException)
		{}
		
		protected ParseException (SerializationInfo info, StreamingContext context)
			: base (info, context)
		{}
	}

	public class Parser {

		private class TokenQueue : Queue<Token> {}

		public abstract class ParseNode {
			public ParseNode(Parser p) { }
		}

		// Node = Expression {Newline Expression}
		public class Node : ParseNode {
			List<Expression> expressions = new List<Expression>();

			public Node(Parser p) : base(p) {
				expressions.Add(new Expression(p));

				p.ExpectSymbol(TokenType.EndOfLine);

				while (p.NextSymbolIs(TokenType.EndOfInput) == false) {
					expressions.Add(new Expression(p));

					p.ExpectSymbol(TokenType.EndOfLine);

				}
				
			}
			public override string ToString ()
			{
				string s = "Node: \n";
				foreach (var expression in expressions) {
					s += expression.ToString() + "\n";
				}
				return s;
			}
		}

		// Expression = Number Operator Number
		// Expression = Number
		// TODO Expression = Expression Operator Expression
		public class Expression : ParseNode {
			Token number;

			Token leftHand;
			Token exprOperator;
			Token rightHand;

			public Expression(Parser p) : base(p) {

				var firstNumber = p.ExpectSymbol(TokenType.Number);

				if (p.NextSymbolIs(Operator.validTokens)) {
					leftHand = firstNumber;
					exprOperator = p.ExpectSymbol(Operator.validTokens);
					rightHand = p.ExpectSymbol(TokenType.Number);
				} else {
					number = firstNumber;
				}
			}

			public override string ToString ()
			{
				if (number != null) {
					return number.ToString();
				} else {
					return string.Format("{0} {1} {2}", leftHand, exprOperator, rightHand);
				}

			}
		}

		public class Operator : ParseNode {
			TokenType operatorType;

			public static  TokenType[] validTokens {
				get {
					return new TokenType[] {
						TokenType.Add,
						TokenType.Minus,
						TokenType.Divide,
						TokenType.Multiply,
						
						TokenType.AddAssign,
						TokenType.MinusAssign,
						TokenType.DivideAssign,
						TokenType.MultiplyAssign,						
					};
				}
			}

			public Operator(Parser p) : base(p) {
				operatorType = p.ExpectSymbol(Operator.validTokens).type;
			}

			public override string ToString ()
			{
				return operatorType.ToString();
			}
		}

		Queue<Token> tokens;

		public Parser(ICollection<Token> tokens) {
			this.tokens = new Queue<Token>(tokens);
		}

		public Node Parse() {

			return new Node(this);

		}

		bool NextSymbolIs(TokenType type) {
			return this.tokens.Peek().type == type;
		}

		bool NextSymbolIs(params TokenType[] validTypes) {
			var t = this.tokens.Peek().type;

			foreach (var validType in validTypes) {
				if (t == validType) {
					return true;
				}
			}
			return false;
		}

		TokenType PeekNextToken() {
			return this.tokens.Peek().type;
		}

		Token ExpectSymbol(TokenType type) {
			var t = this.tokens.Dequeue();
			if (t.type != type) {
				throw ParseException.Make(t, type);
			}
			return t;
		}

		Token ExpectSymbol(params TokenType[] validTypes) {
			var t = this.tokens.Dequeue();

			foreach (var validType in validTypes) {
				if (t.type == validType) {
					return t;
				}
			}

			throw ParseException.Make(t, validTypes);
		}


		public bool HasTokensRemaining {
			get {
				return this.tokens.Count > 0;
			}
		}



	}


}