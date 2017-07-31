/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

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
using System.Runtime.Serialization;
using System.Text;

namespace Yarn {

    // Magic abstract syntax tree producer - feed it tokens, and it gives you
    // a tree representation! Or an error!
    internal class Parser {

        // Indents the 'input' string 'indentLevel' times
        private static string Tab(int indentLevel, string input, bool newLine = true) {
            var sb = new StringBuilder();

            for (int i = 0; i < indentLevel; i++) {
                sb.Append ("| ");
            }
            sb.Append (input);

            if (newLine)
                sb.Append ("\n");

            return sb.ToString ();
        }

        #region Parse Nodes
        // Base class for nodes in th parse tree
        internal abstract class ParseNode {

            internal ParseNode parent;

            // The line that this parse node begins on.
            internal int lineNumber;

            // ParseNodes do their parsing by consuming tokens from the Parser.
            // You parse tokens into a ParseNode by using its constructor.
            internal ParseNode(ParseNode parent, Parser p) {
                this.parent = parent;
                if (p.tokens.Count > 0)
                    this.lineNumber = p.tokens.Peek().lineNumber;
                else
                    this.lineNumber = -1;
            }

            // Recursively prints the ParseNode and all of its child ParseNodes.
            internal abstract string PrintTree (int indentLevel);

            internal string[] tags = {};

            public string TagsToString(int indentLevel)
            {
                if (tags.Length > 0) {
                    var s = new StringBuilder ();

                    s.Append (Tab (indentLevel + 1, "Tags:"));
                    foreach (var tag in this.tags) {
                        s.Append(Tab (indentLevel + 2, "#" + tag));
                    }
                    return s.ToString ();
                } else {
                    return "";
                }

            }

            public override string ToString ()
            {
                return this.GetType ().Name;
            }

            // The closest parent to this ParseNode that is a Node.
            internal Node NodeParent() {
                var node = this;

                do {
                    if (node is Node) {
                        return node as Node;
                    }
                    node = node.parent;
                } while (node
                    != null);

                return null;
            }
        }

        // The top-level unit of parsing.
        // Node = (Statement)* EndOfInput
        internal class Node : ParseNode {

            internal string name { get; set;}

            internal string source { get; set; }

            // defined in the Yarn editor
            internal List<string> nodeTags { get; set; }

            // Read-only internal accessor for statements
            internal IEnumerable<Statement> statements { get { return _statements; }}

            // The statements in this node
            List<Statement> _statements = new List<Statement> ();

            internal Node(string name, ParseNode parent, Parser p) : base(parent, p) {
                this.name = name;
                // Consume statements until we run out of input or we hit a dedent
                while (p.tokens.Count > 0 && p.NextSymbolIs(TokenType.Dedent,TokenType.EndOfInput) == false) {
                    _statements.Add(new Statement(this, p));
                }

            }

            // Print the statements we have
            internal override string PrintTree (int indentLevel)
            {
                var sb = new StringBuilder ();
                sb.Append (Tab (indentLevel, "Node "+name+" {"));
                foreach (var statement in _statements) {
                    sb.Append( statement.PrintTree (indentLevel + 1));
                }
                sb.Append (Tab (indentLevel, "}"));
                return sb.ToString();
            }

        }

        // Statements are the items of execution in nodes.
        // Statement = Block
        // Statement = IfStatement
        // Statement = OptionStatement
        // Statement = ShortcutOptionGroup
        // Statement = CustomCommand
        // Statement = AssignmentStatement
        // Statement = <Text>
        internal class Statement : ParseNode {

            internal enum Type {
                CustomCommand,
                ShortcutOptionGroup,
                Block,
                IfStatement,
                OptionStatement,
                AssignmentStatement,
                Line
            }

            internal Statement.Type type { get; private set; }

            // The possible types of statements we can have
            internal Block block { get; private set;}
            internal IfStatement ifStatement {get; private set;}
            internal OptionStatement optionStatement {get; private set;}
            internal AssignmentStatement assignmentStatement {get; private set;}
            internal CustomCommand customCommand {get;private set;}
            internal string line {get; private set;}
            internal ShortcutOptionGroup shortcutOptionGroup { get; private set; }

            internal Statement(ParseNode parent, Parser p) : base(parent, p) {

                if (Block.CanParse(p)) {
                    type = Type.Block;
                    block = new Block(this, p);
                } else if (IfStatement.CanParse(p)) {
                    type = Type.IfStatement;
                    ifStatement = new IfStatement(this, p);
                } else if (OptionStatement.CanParse(p)) {
                    type = Type.OptionStatement;
                    optionStatement = new OptionStatement(this, p);
                } else if (AssignmentStatement.CanParse(p)) {
                    type = Type.AssignmentStatement;
                    assignmentStatement = new AssignmentStatement(this, p);
                } else if (ShortcutOptionGroup.CanParse(p)) {
                    type = Type.ShortcutOptionGroup;
                    shortcutOptionGroup = new ShortcutOptionGroup(this, p);
                } else if (CustomCommand.CanParse(p)) {
                    type = Type.CustomCommand;
                    customCommand = new CustomCommand(this, p);
                } else if (p.NextSymbolIs(TokenType.Text)) {
                    line = p.ExpectSymbol(TokenType.Text).value as string;
                    type = Type.Line;
                } else {
                    throw ParseException.Make(p.tokens.Peek(), "Expected a statement here but got " + p.tokens.Peek().ToString() +" instead (was there an unbalanced if statement earlier?)");
                }

                // Parse the optional tags that follow this statement
                var tags = new List<string>();

                while (p.NextSymbolIs(TokenType.TagMarker)) {
                    p.ExpectSymbol(TokenType.TagMarker);
                    var tag = p.ExpectSymbol(TokenType.Identifier).value;
                    tags.Add(tag);
                }

                if (tags.Count > 0)
                    this.tags = tags.ToArray();
            }

            internal override string PrintTree (int indentLevel)
            {
                StringBuilder s = new StringBuilder ();
                switch (type) {
                case Type.Block:
                    s.Append(block.PrintTree (indentLevel));
                    break;
                case Type.IfStatement:
                    s.Append (ifStatement.PrintTree (indentLevel));
                    break;
                case Type.OptionStatement:
                    s.Append (optionStatement.PrintTree (indentLevel));
                    break;
                case Type.AssignmentStatement:
                    s.Append (assignmentStatement.PrintTree (indentLevel));
                    break;
                case Type.ShortcutOptionGroup:
                    s.Append (shortcutOptionGroup.PrintTree (indentLevel));
                    break;
                case Type.CustomCommand:
                    s.Append (customCommand.PrintTree (indentLevel));
                    break;
                case Type.Line:
                    s.Append (Tab (indentLevel, "Line: " + line));
                    break;
                default:
                    throw new ArgumentNullException ();
                }

                s.Append (TagsToString (indentLevel));

                return s.ToString ();
            }

        }

        // Custom commands are meant to be interpreted by whatever
        // system that owns this dialogue sytem. eg <<stand>>
        // CustomCommand = BeginCommand <ANY>* EndCommand
        internal class CustomCommand : ParseNode {

            internal enum Type {
                Expression,
                ClientCommand
            }

            internal Type type;

            internal Expression expression {get; private set;}
            internal string clientCommand { get; private set;}

            internal static bool CanParse (Parser p)
            {
                return p.NextSymbolsAre (TokenType.BeginCommand, TokenType.Text) ||
                    p.NextSymbolsAre (TokenType.BeginCommand, TokenType.Identifier);
            }

            internal CustomCommand(ParseNode parent, Parser p) : base(parent, p) {

                p.ExpectSymbol(TokenType.BeginCommand);

                // Custom commands can have ANY token in them. Read them all until we hit the
                // end command token.
                var commandTokens = new List<Token>();
                do {
                    commandTokens.Add(p.ExpectSymbol());
                } while (p.NextSymbolIs(TokenType.EndCommand) == false);
                p.ExpectSymbol(TokenType.EndCommand);

                // If the first token is an identifier and the second is
                // a left paren, it may be a function call expression;
                // evaluate it as such
                if (commandTokens.Count > 1 &&
                    commandTokens[0].type == TokenType.Identifier &&
                    commandTokens[1].type == TokenType.LeftParen) {
                    var parser = new Parser(commandTokens, p.library);
                    var expression = Expression.Parse(this, parser);
                    type = Type.Expression;
                    this.expression = expression;
                } else {
                    // Otherwise, evaluate it as a command
                    type = Type.ClientCommand;

                    this.clientCommand = commandTokens[0].value;
                }

            }

            internal override string PrintTree (int indentLevel)
            {
                switch (type) {
                case Type.Expression:
                    return Tab (indentLevel, "Expression: ") + expression.PrintTree (indentLevel + 1);
                case Type.ClientCommand:
                    return Tab (indentLevel, "Command: " + clientCommand);
                }
                return "";

            }
        }

        // Shortcut option groups are groups of shortcut options,
        // followed by the node that they rejoin.
        // ShortcutOptionGroup = ShortcutOption+ Node
        internal class ShortcutOptionGroup : ParseNode {
            internal static bool CanParse (Parser p)
            {
                return p.NextSymbolIs (TokenType.ShortcutOption);
            }

            internal IEnumerable<ShortcutOption> options { get { return _options; }}

            // The options in this group
            private List<ShortcutOption> _options = new List<ShortcutOption>();

            internal ShortcutOptionGroup(ParseNode parent, Parser p) : base(parent, p) {

                // keep parsing options until we can't, but expect at least one (otherwise it's
                // not actually a list of options)
                int shortcutIndex = 1; // give each option a number so it can name itself
                do {
                    _options.Add(new ShortcutOption(shortcutIndex++, this, p));
                } while (p.NextSymbolIs(TokenType.ShortcutOption));
            }

            internal override string PrintTree (int indentLevel)
            {
                var sb = new StringBuilder ();
                sb.Append (Tab (indentLevel, "Shortcut option group {"));

                foreach (var option in options) {
                    sb.Append (option.PrintTree (indentLevel + 1));
                }
                sb.Append (Tab (indentLevel, "}"));

                return sb.ToString ();
            }

        }

        // Shortcut options are a convenient way to define new options.
        // ShortcutOption = -> <text> [BeginCommand If Expression EndCommand] [Block]
        internal class ShortcutOption : ParseNode {
            internal string label { get; private set;}
            internal Expression condition { get; private set;}
            internal Node optionNode { get; private set;}

            internal ShortcutOption(int optionIndex, ParseNode parent, Parser p) : base(parent, p) {
                p.ExpectSymbol(TokenType.ShortcutOption);
                label = p.ExpectSymbol(TokenType.Text).value as string;

                // Parse the conditional ("<<if $foo>>") if it's there

                var tags = new List<string>();

                while (
                    p.NextSymbolsAre(TokenType.BeginCommand, TokenType.If) ||
                    p.NextSymbolIs(TokenType.TagMarker)) {

                    if (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.If)) {
                        p.ExpectSymbol(TokenType.BeginCommand);
                        p.ExpectSymbol(TokenType.If);
                        condition = Expression.Parse(this, p);
                        p.ExpectSymbol(TokenType.EndCommand);
                    } else if (p.NextSymbolIs(TokenType.TagMarker)) {

                        p.ExpectSymbol(TokenType.TagMarker);
                        var tag = p.ExpectSymbol(TokenType.Identifier).value;
                        tags.Add(tag);
                    }
                }

                this.tags = tags.ToArray();

                // Parse the statements belonging to this option if it has any
                if (p.NextSymbolIs(TokenType.Indent)) {
                    p.ExpectSymbol(TokenType.Indent);
                    optionNode = new Node(NodeParent().name + "." + optionIndex, this, p);
                    p.ExpectSymbol(TokenType.Dedent);
                }

            }

            internal override string PrintTree (int indentLevel)
            {
                var sb = new StringBuilder ();
                sb.Append (Tab (indentLevel, "Option \"" +label + "\""));

                if (condition != null) {
                    sb.Append (Tab (indentLevel + 1, "(when:"));
                    sb.Append (condition.PrintTree(indentLevel+2));
                    sb.Append (Tab (indentLevel + 1, "),"));
                }

                if (optionNode != null) {
                    sb.Append (Tab (indentLevel, "{"));
                    sb.Append (optionNode.PrintTree (indentLevel + 1));
                    sb.Append (Tab (indentLevel, "}"));
                }

                sb.Append (TagsToString (indentLevel));

                return sb.ToString ();
            }

        }

        // Blocks are indented groups of statements
        // Block = Indent Statement* Dedent
        internal class Block : ParseNode {
            internal static bool CanParse (Parser p)
            {
                return p.NextSymbolIs (TokenType.Indent);
            }


            internal IEnumerable<Statement> statements { get { return _statements; }}

            List<Statement> _statements = new List<Statement> ();

            internal Block(ParseNode parent, Parser p) : base(parent, p) {

                // Read the indent token
                p.ExpectSymbol(TokenType.Indent);

                // Keep reading statements until we hit a dedent
                while (p.NextSymbolIs(TokenType.Dedent) == false) {
                    // fun fact! because Blocks are a type of Statement,
                    // we get nested block parsing for free! \:D/
                    _statements.Add(new Statement(this, p));
                }

                // Tidy up by reading the dedent
                p.ExpectSymbol(TokenType.Dedent);

            }

            internal override string PrintTree (int indentLevel)
            {
                var sb = new StringBuilder ();
                sb.Append (Tab(indentLevel, "Block {"));
                foreach (var statement in _statements) {
                    sb.Append (statement.PrintTree (indentLevel + 1));
                }
                sb.Append (Tab(indentLevel, "}"));

                return sb.ToString ();
            }
        }

        // Options are links to other nodes
        // OptionStatement = OptionStart <Text> OptionEnd
        // OptionStatement = OptionStart <Text> OptionDelimit <Text>|<Identifier> OptionEnd
        internal class OptionStatement : ParseNode {
            internal static bool CanParse (Parser p)
            {
                return p.NextSymbolIs (TokenType.OptionStart);
            }

            internal string destination { get; private set;}
            internal string label { get; private set;}

            internal OptionStatement(ParseNode parent, Parser p) : base(parent, p) {

                // The meaning of the string(s) we have changes
                // depending on whether we have one or two, so
                // keep them both and decide their meaning once
                // we know more

                string firstString;
                string secondString;

                // Parse "[[LABEL"
                p.ExpectSymbol(TokenType.OptionStart);
                firstString = p.ExpectSymbol(TokenType.Text).value as String;

                // If there's a | in there, get the string that comes after it
                if (p.NextSymbolIs(TokenType.OptionDelimit)) {

                    p.ExpectSymbol(TokenType.OptionDelimit);
                    secondString = p.ExpectSymbol(TokenType.Text, TokenType.Identifier).value as String;

                    // Two strings mean that the first is the label, and the second
                    // is the name of the node that we should head to if this option
                    // is selected
                    label = firstString;
                    destination = secondString;
                } else {
                    // One string means we don't have a label
                    label = null;
                    destination = firstString;
                }

                // Parse the closing ]]
                p.ExpectSymbol(TokenType.OptionEnd);
            }

            internal override string PrintTree (int indentLevel)
            {
                if (label != null) {
                    return Tab (indentLevel, string.Format ("Option: \"{0}\" -> {1}", label, destination));
                } else {
                    return Tab (indentLevel, string.Format ("Option: -> {0}", destination));
                }
            }
        }

        // If statements are the usual if-else-elseif-endif business.
        // If = BeginCommand If Expression EndCommand Statement* BeginCommand EndIf EndCommand
        // TODO: elseif
        internal class IfStatement : ParseNode {
            internal static bool CanParse (Parser p)
            {
                return p.NextSymbolsAre (TokenType.BeginCommand, TokenType.If);
            }

            // Clauses are collections of statements with an
            // optional conditional that determines whether they're run
            // or not. The condition is used by the If and ElseIf parts of
            // an if statement, and not used by the Else statement.
            internal struct Clause {
                internal Expression expression;
                internal IEnumerable<Statement> statements;
                internal string PrintTree(int indentLevel) {
                    var sb = new StringBuilder ();
                    if (expression != null)
                        sb.Append (expression.PrintTree (indentLevel));
                    sb.Append (Tab (indentLevel, "{"));
                    foreach (var statement in statements) {
                        sb.Append (statement.PrintTree (indentLevel + 1));
                    }
                    sb.Append (Tab (indentLevel, "}"));
                    return sb.ToString ();
                }
            }

            internal List<Clause> clauses = new List<Clause>();

            internal IfStatement(ParseNode parent, Parser p) : base(parent, p) {

                // All if statements begin with "<<if EXPRESSION>>", so parse that
                Clause primaryClause = new Clause();

                p.ExpectSymbol(TokenType.BeginCommand);
                p.ExpectSymbol(TokenType.If);
                primaryClause.expression = Expression.Parse(this, p);
                p.ExpectSymbol(TokenType.EndCommand);

                // Read the statements for this clause until  we hit an <<endif or <<else
                // (which could be an "<<else>>" or an "<<else if"
                var statements = new List<Statement>();
                while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false &&
                    p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else) == false &&
                    p.NextSymbolsAre(TokenType.BeginCommand, TokenType.ElseIf) == false) {
                    statements.Add(new Statement(this, p));

                    // Ignore any dedents
                    while (p.NextSymbolIs(TokenType.Dedent)) {
                        p.ExpectSymbol(TokenType.Dedent);
                    }
                }
                primaryClause.statements = statements;

                clauses.Add(primaryClause);

                // Handle as many <<elseif clauses as we find
                while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.ElseIf)) {
                    var elseIfClause = new Clause();

                    // Parse the syntax for this clause's condition
                    p.ExpectSymbol(TokenType.BeginCommand);
                    p.ExpectSymbol(TokenType.ElseIf);
                    elseIfClause.expression = Expression.Parse(this, p);
                    p.ExpectSymbol(TokenType.EndCommand);

                    // Read statements until we hit an <<endif, <<else or another <<elseif
                    var clauseStatements = new List<Statement>();
                    while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false &&
                        p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else) == false &&
                        p.NextSymbolsAre(TokenType.BeginCommand, TokenType.ElseIf) == false) {
                        clauseStatements.Add(new Statement(this, p));

                        // Ignore any dedents
                        while (p.NextSymbolIs(TokenType.Dedent)) {
                            p.ExpectSymbol(TokenType.Dedent);
                        }

                    }

                    elseIfClause.statements = clauseStatements;

                    clauses.Add(elseIfClause);
                }

                // Handle <<else>> if we have it
                if (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else, TokenType.EndCommand)) {

                    // parse the syntax (no expression this time, just "<<else>>"
                    p.ExpectSymbol(TokenType.BeginCommand);
                    p.ExpectSymbol(TokenType.Else);
                    p.ExpectSymbol(TokenType.EndCommand);

                    // and parse statements until we hit "<<endif"
                    var elseClause = new Clause();
                    var clauseStatements = new List<Statement>();
                    while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false) {
                        clauseStatements.Add(new Statement(this, p));
                    }
                    elseClause.statements = clauseStatements;

                    this.clauses.Add(elseClause);

                    // Ignore any dedents
                    while (p.NextSymbolIs(TokenType.Dedent)) {
                        p.ExpectSymbol(TokenType.Dedent);
                    }

                }

                // Finish up by reading the <<endif>>
                p.ExpectSymbol(TokenType.BeginCommand);
                p.ExpectSymbol(TokenType.EndIf);
                p.ExpectSymbol(TokenType.EndCommand);
            }

            internal override string PrintTree (int indentLevel)
            {
                var sb = new StringBuilder ();
                var first = true;
                foreach (var clause in clauses) {
                    if (first) {
                        sb.Append (Tab (indentLevel, "If:"));
                        first = false;
                    } else if (clause.expression != null) {
                        sb.Append (Tab (indentLevel, "Else If:"));
                    } else {
                        sb.Append (Tab (indentLevel, "Else:"));
                    }
                    sb.Append (clause.PrintTree (indentLevel + 1));
                }

                return sb.ToString ();
            }
        }

        // A value, which forms part of an expression.
        public class ValueNode : ParseNode {

            public Value value { get; private set;}

            private void UseToken(Token t) {
                // Store the value depending on token's type
                switch (t.type) {
                case TokenType.Number:

                    value = new Value (float.Parse (t.value as String));

                    break;
                case TokenType.String:

                    value = new Value (t.value as String);
                    break;

                case TokenType.False:

                    value = new Value (false);

                    break;
                case TokenType.True:

                    value = new Value (true);

                    break;
                case TokenType.Variable:
                    value = new Value ();
                    value.type = Value.Type.Variable;
                    value.variableName = t.value as String;
                    break;
                case TokenType.Null:
                    value = Value.NULL;
                    break;
                default:
                    throw ParseException.Make (t, "Invalid token type " + t.ToString ());
                }
            }

            // Use a provided token
            internal ValueNode(ParseNode parent, Token t, Parser p) : base (parent, p) {
                UseToken(t);
            }

            // Read a number or a variable name from the parser
            internal ValueNode(ParseNode parent, Parser p) : base(parent, p) {

                Token t = p.ExpectSymbol(TokenType.Number, TokenType.Variable, TokenType.String);

                UseToken(t);
            }

            internal override string PrintTree (int indentLevel)
            {
                switch (value.type) {
                case Value.Type.Number:
                    return Tab (indentLevel, value.numberValue.ToString());
                case Value.Type.String:
                    return Tab(indentLevel, String.Format("\"{0}\"", value.stringValue));
                case Value.Type.Bool:
                    return Tab (indentLevel, value.boolValue.ToString());
                case Value.Type.Variable:
                    return Tab (indentLevel, value.variableName);
                case Value.Type.Null:
                    return Tab (indentLevel, "(null)");
                }
                throw new ArgumentException ();
            }
        }

        // Expressions are things like "1 + 2 * 5 / 2 - 1"
        // Expression = Expression Operator Expression
        // Expression = Identifier ( Expression [, Expression]* )
        // Expression = Value
        internal class Expression : ParseNode {

            internal enum Type {
                Value,
                FunctionCall
            }

            internal Type type;

            internal ValueNode value;
            // - or -
            internal FunctionInfo function;
            internal List<Expression> parameters;

            internal Expression(ParseNode parent, ValueNode value, Parser p) : base(parent, p) {
                this.type = Type.Value;
                this.value = value;
            }

            internal Expression(ParseNode parent, FunctionInfo function, List<Expression> parameters, Parser p) : base(parent, p) {
                type = Type.FunctionCall;
                this.function = function;
                this.parameters = parameters;
            }

            internal static Expression Parse(ParseNode parent, Parser p) {

                // Applies Djikstra's "shunting-yard" algorithm to convert the
                // stream of infix expressions into postfix notation; we then
                // build a tree of expressions from the result

                // https://en.wikipedia.org/wiki/Shunting-yard_algorithm

                Queue<Token> _expressionRPN = new Queue<Token> ();
                var operatorStack = new Stack<Token>();

                // used for keeping count of parameters for each function
                var functionStack = new Stack<Token> ();

                var allValidTokenTypes = new List<TokenType>(Operator.OperatorTypes);
                allValidTokenTypes.Add(TokenType.Number);
                allValidTokenTypes.Add(TokenType.Variable);
                allValidTokenTypes.Add(TokenType.String);
                allValidTokenTypes.Add(TokenType.LeftParen);
                allValidTokenTypes.Add(TokenType.RightParen);
                allValidTokenTypes.Add(TokenType.Identifier);
                allValidTokenTypes.Add(TokenType.Comma);
                allValidTokenTypes.Add(TokenType.True);
                allValidTokenTypes.Add(TokenType.False);
                allValidTokenTypes.Add(TokenType.Null);

                Token lastToken = null;

                // Read all the contents of the expression
                while (p.tokens.Count > 0 && p.NextSymbolIs(allValidTokenTypes.ToArray())) {

                    Token nextToken = p.ExpectSymbol(allValidTokenTypes.ToArray());

                    if (nextToken.type == TokenType.Number ||
                        nextToken.type == TokenType.Variable ||
                        nextToken.type == TokenType.String ||
                        nextToken.type == TokenType.True ||
                        nextToken.type == TokenType.False ||
                        nextToken.type == TokenType.Null) {

                        // Primitive values go straight onto the output
                        _expressionRPN.Enqueue (nextToken);
                    } else if (nextToken.type == TokenType.Identifier) {
                        operatorStack.Push (nextToken);
                        functionStack.Push (nextToken);

                        // next token must be a left paren, so process that immediately
                        nextToken = p.ExpectSymbol (TokenType.LeftParen);
                        // enter that sub-expression
                        operatorStack.Push (nextToken);

                    } else if (nextToken.type == TokenType.Comma) {

                        // Resolve this sub-expression before moving on to the
                        // next parameter
                        try {
                            // pop operators until we reach a left paren
                            while (operatorStack.Peek().type != TokenType.LeftParen) {
                                _expressionRPN.Enqueue(operatorStack.Pop());
                            }
                        } catch (InvalidOperationException) {
                            // we reached the end of the stack prematurely
                            // this means unbalanced parens!
                            throw ParseException.Make(nextToken, "Error parsing expression: " +
                                "unbalanced parentheses");
                        }

                        // We expect the top of the stack to now contain the left paren that
                        // began the list of parameters
                        if (operatorStack.Peek().type != TokenType.LeftParen) {
                            throw ParseException.Make (operatorStack.Peek (), "Expression parser got " +
                                "confused dealing with a function");
                        }

                        // The next token is not allowed to be a right-paren or a comma
                        // (that is, you can't say "foo(2,,)")
                        if (p.NextSymbolIs(TokenType.RightParen, TokenType.Comma)) {
                            throw ParseException.Make (p.tokens.Peek(), "Expected expression");
                        }

                        // Find the closest function on the stack
                        // and increment the number of parameters
                        functionStack.Peek().parameterCount++;

                    } else if (Operator.IsOperator(nextToken.type)) {
                        // This is an operator

                        // If this is a Minus, we need to determine if it's a
                        // unary minus or a binary minus.
                        // Unary minus looks like this: "-1"
                        // Binary minus looks like this: "2 - 3"
                        // Things get complex when we say stuff like "1 + -1".
                        // But it's easier when we realise that a minus
                        // is ONLY unary when the last token was a left paren,
                        // an operator, or it's the first token.

                        if (nextToken.type == TokenType.Minus) {

                            if (lastToken == null ||
                                lastToken.type == TokenType.LeftParen ||
                                Operator.IsOperator(lastToken.type)) {

                                // This is actually a unary minus.
                                nextToken.type = TokenType.UnaryMinus;
                            }
                        }

                        // We cannot assign values inside an expression. That is,
                        // saying "$foo = 2" in an express does not assign $foo to 2
                        // and then evaluate to 2. Instead, Yarn defines this
                        // to mean "$foo == 2"
                        if (nextToken.type == TokenType.EqualToOrAssign) {
                            nextToken.type = TokenType.EqualTo;
                        }

                        // O1 = this operator
                        // O2 = the token at the top of the stack
                        // While O2 is an operator, and EITHER: 1. O1 is left-associative and
                        // has precedence <= O2, or 2. O1 is right-associative and
                        // has precedence > O2:
                        while (ShouldApplyPrecedence(nextToken.type, operatorStack)) {
                            var o = operatorStack.Pop();
                            _expressionRPN.Enqueue(o);
                        }
                        operatorStack.Push(nextToken);

                    } else if (nextToken.type == TokenType.LeftParen) {

                        // Record that we have entered a paren-delimited
                        // subexpression
                        operatorStack.Push(nextToken);

                    } else if (nextToken.type == TokenType.RightParen) {

                        // We're leaving a subexpression; time to resolve the
                        // order of operations that we saw in between the parens.

                        try {
                            // pop operators until we reach a left paren
                            while (operatorStack.Peek().type != TokenType.LeftParen) {
                                _expressionRPN.Enqueue(operatorStack.Pop());
                            }
                            // pop the left paren
                            operatorStack.Pop();
                        } catch (InvalidOperationException) {
                            // we reached the end of the stack prematurely
                            // this means unbalanced parens!
                            throw ParseException.Make(nextToken, "Error parsing expression: unbalanced parentheses");
                        }

                        if (operatorStack.Peek().type == TokenType.Identifier) {
                            // This whole paren-delimited subexpression is actually
                            // a function call

                            // If the last token was a left-paren, then this
                            // was a function with no parameters; otherwise, we
                            // have an additional parameter (on top of the ones we counted
                            // while encountering commas)

                            if (lastToken.type != TokenType.LeftParen) {
                                functionStack.Peek ().parameterCount++;
                            }

                            _expressionRPN.Enqueue(operatorStack.Pop());
                            functionStack.Pop ();
                        }

                    }

                    // Record this as the last token we saw; we'll use
                    // this to figure out if minuses are unary or not
                    lastToken = nextToken;

                }

                // No more tokens; pop all operators onto the output queue
                while (operatorStack.Count > 0) {
                    _expressionRPN.Enqueue(operatorStack.Pop());
                }

                // If the output queue is empty, then this is not an expression
                if (_expressionRPN.Count == 0) {
                    throw new ParseException ("Error parsing expression: no expression found!");
                }

                // We've now got this in more easily parsed RPN form;
                // time to build the expression tree.
                Token firstToken = _expressionRPN.Peek();
                var evaluationStack = new Stack<Expression>();
                while (_expressionRPN.Count > 0) {

                    var next = _expressionRPN.Dequeue();
                    if (Operator.IsOperator(next.type)) {

                        // This is an operation

                        var info = Operator.InfoForOperator(next.type);
                        if (evaluationStack.Count < info.arguments) {
                            throw ParseException.Make(next, "Error parsing expression: not enough " +
                                "arguments for operator "+next.type.ToString());
                        }

                        var parameters = new List<Expression> ();

                        for (int i = 0; i < info.arguments; i++) {
                            parameters.Add (evaluationStack.Pop ());
                        }
                        parameters.Reverse ();

                        var operatorFunc = p.library.GetFunction (next.type.ToString());

                        var expr = new Expression (parent, operatorFunc, parameters, p);

                        evaluationStack.Push(expr);
                    } else if (next.type == TokenType.Identifier) {
                        // This is a function call

                        FunctionInfo info = null;

                        // If we have a library, use it to check if the
                        // number of parameters provided is correct
                        if (p.library != null) {
                            info = p.library.GetFunction(next.value as String);

                            // Ensure that this call has the right number of params
                            if (info.IsParameterCountCorrect(next.parameterCount) == false) {
                                string error = string.Format("Error parsing expression: " +
                                    "Unsupported number of parameters for function {0} (expected {1}, got {2})",
                                    next.value as String,
                                    info.paramCount,
                                    next.parameterCount
                                );
                                throw ParseException.Make(next, error);
                            }
                        } else {
                            // Use a dummy FunctionInfo to represent info about the
                            // fact that a function is called; note that
                            // attempting to call this will fail
                            info = new FunctionInfo (next.value, next.parameterCount, (Function)null);
                        }

                        var parameterList = new List<Expression> ();
                        for (int i = 0; i < next.parameterCount; i++) {
                            parameterList.Add (evaluationStack.Pop());
                        }
                        parameterList.Reverse ();

                        var expr = new Expression (parent, info, parameterList, p);

                        evaluationStack.Push (expr);

                    } else {

                        // This is a raw value
                        var v = new ValueNode(parent, next, p);
                        Expression expr = new Expression(parent, v, p);
                        evaluationStack.Push(expr);

                    }
                }
                // We should now have a single expression in this stack, which is the root
                // of the expression's tree. If we have more than one, then we have a problem.
                if (evaluationStack.Count != 1) {
                    throw ParseException.Make(firstToken, "Error parsing expression " +
                        "(stack did not reduce correctly)");
                }

                // Return it
                return evaluationStack.Pop ();

            }

            // Used to determine whether the shunting-yard algorithm should pop operators from
            // the operator stack.
            private static bool ShouldApplyPrecedence(TokenType o1, Stack<Token> operatorStack) {

                if (operatorStack.Count == 0) {
                    return false;
                }
                if (Operator.IsOperator (o1) == false) {
                    throw new ParseException ("Internal error parsing expression");
                }
                TokenType o2 = operatorStack.Peek ().type;

                if (Operator.IsOperator (o2) == false)
                    return false;

                var o1Info = Operator.InfoForOperator (o1);
                var o2Info = Operator.InfoForOperator (o2);

                if (o1Info.associativity == Operator.Associativity.Left && o1Info.precedence <= o2Info.precedence) {
                    return true;
                }

                if (o1Info.associativity == Operator.Associativity.Right && o1Info.precedence < o2Info.precedence) {
                    return true;
                }

                return false;
            }

            internal override string PrintTree (int indentLevel)
            {
                var stringBuilder = new StringBuilder ();
                switch (type) {
                case Type.Value:
                    return value.PrintTree (indentLevel);
                case Type.FunctionCall:

                    if (parameters.Count == 0) {
                        stringBuilder.Append(Tab(indentLevel, "Function call to " + function.name + " (no parameters)"));
                    } else {
                        stringBuilder.Append(Tab(indentLevel, "Function call to " + function.name + " (" +parameters.Count+" parameters) {"));
                        foreach (var param in parameters) {
                            stringBuilder.Append(param.PrintTree(indentLevel+1));
                        }
                        stringBuilder.Append(Tab(indentLevel, "}"));
                    }
                    return stringBuilder.ToString();

                }

                return Tab(indentLevel, "<error printing expression!>");
            }
        }

        // AssignmentStatements are things like <<set $foo = 1>>
        // AssignmentStatement = BeginCommand Set <variable> <operation> Expression EndCommand
        internal class AssignmentStatement : ParseNode {
            internal static bool CanParse (Parser p)
            {
                return p.NextSymbolsAre (TokenType.BeginCommand, TokenType.Set);
            }

            internal string destinationVariableName { get; private set; }

            internal Expression valueExpression { get; private set; }

            internal TokenType operation { get; private set; }

            private static TokenType[] validOperators = {
                TokenType.EqualToOrAssign,
                TokenType.AddAssign,
                TokenType.MinusAssign,
                TokenType.DivideAssign,
                TokenType.MultiplyAssign
            };

            internal AssignmentStatement(ParseNode parent, Parser p) : base(parent, p) {

                p.ExpectSymbol(TokenType.BeginCommand);
                p.ExpectSymbol(TokenType.Set);
                destinationVariableName = p.ExpectSymbol(TokenType.Variable).value as string;
                operation = p.ExpectSymbol(validOperators).type;
                valueExpression = Expression.Parse(this, p);
                p.ExpectSymbol(TokenType.EndCommand);

            }

            internal override string PrintTree (int indentLevel)
            {
                var sb = new StringBuilder ();
                sb.Append (Tab(indentLevel, "Set:"));
                sb.Append (Tab(indentLevel+1, destinationVariableName));
                sb.Append (Tab (indentLevel+1,  operation.ToString()));
                sb.Append (valueExpression.PrintTree (indentLevel + 1));
                return sb.ToString ();

            }
        }

        // Operators are used in expressions - things like + - / * != neq
        internal class Operator : ParseNode {
            internal TokenType operatorType { get; private set; }

            internal enum Associativity {
                Left, // resolve leftmost operand first
                Right, // resolve rightmost operand first
                None // special-case (like "(", ")", ","
            }

            // Info used during expression parsing
            internal struct OperatorInfo {
                internal Associativity associativity;
                internal int precedence;
                internal int arguments;
                internal OperatorInfo(Associativity associativity, int precedence, int arguments) {
                    this.associativity = associativity;
                    this.precedence = precedence;
                    this.arguments = arguments;
                }
            }

            internal static OperatorInfo InfoForOperator(TokenType op) {
                if (Array.IndexOf(OperatorTypes, op) == -1) {
                    throw new ParseException (op.ToString () + " is not a valid operator");
                }

                // Determine the precendence, associativity and
                // number of operands that each operator has.
                switch (op) {

                case TokenType.Not:
                case TokenType.UnaryMinus:
                    return new OperatorInfo (Associativity.Right, 30, 1);

                case TokenType.Multiply:
                case TokenType.Divide:
                case TokenType.Modulo:
                    return new OperatorInfo(Associativity.Left, 20,2);
                case TokenType.Add:
                case TokenType.Minus:
                    return new OperatorInfo(Associativity.Left, 15,2);
                case TokenType.GreaterThan:
                case TokenType.LessThan:
                case TokenType.GreaterThanOrEqualTo:
                case TokenType.LessThanOrEqualTo:
                    return new OperatorInfo(Associativity.Left, 10,2);
                case TokenType.EqualTo:
                case TokenType.EqualToOrAssign:
                case TokenType.NotEqualTo:
                    return new OperatorInfo(Associativity.Left, 5,2);
                case TokenType.And:
                    return new OperatorInfo(Associativity.Left, 4,2);
                case TokenType.Or:
                    return new OperatorInfo(Associativity.Left, 3,2);
                case TokenType.Xor:
                    return new OperatorInfo(Associativity.Left, 2,2);

                }
                throw new InvalidOperationException ("Unknown operator " + op.ToString());

            }

            internal static bool IsOperator(TokenType type) {
                return Array.IndexOf (OperatorTypes, type) != -1;
            }

            // Valid types of operators.
            internal static  TokenType[] OperatorTypes {
                get {
                    return new TokenType[] {

                        TokenType.Not,
                        TokenType.UnaryMinus,

                        TokenType.Add,
                        TokenType.Minus,
                        TokenType.Divide,
                        TokenType.Multiply,
                        TokenType.Modulo,

                        TokenType.EqualToOrAssign,
                        TokenType.EqualTo,
                        TokenType.GreaterThan,
                        TokenType.GreaterThanOrEqualTo,
                        TokenType.LessThan,
                        TokenType.LessThanOrEqualTo,
                        TokenType.NotEqualTo,

                        TokenType.And,
                        TokenType.Or,

                        TokenType.Xor
                    };
                }
            }

            internal Operator(ParseNode parent, TokenType t, Parser p) : base(parent, p) {
                operatorType = t;
            }

            internal Operator(ParseNode parent, Parser p) : base(parent, p) {
                operatorType = p.ExpectSymbol(Operator.OperatorTypes).type;
            }

            internal override string PrintTree (int indentLevel)
            {
                return Tab (indentLevel, operatorType.ToString ());
            }
        }
        #endregion Parse Nodes

        // Use a queue since we're continuously consuming them as
        // we parse
        Queue<Token> tokens;

        Library library;

        // Take whatever we were given and make a queue out of it.
        // If library is null, no checks are made to function calls, and
        // all function calls are assumed to be valid.
        internal Parser(ICollection<Token> tokens, Library library) {
            this.tokens = new Queue<Token>(tokens);
            this.library = library;
        }

        internal Node Parse() {

            // Kick off the parsing process by trying to parse a whole node
            return new Node("Start", null, this);
        }

        // Returns true if the next symbol is one of 'validTypes'
        bool NextSymbolIs(params TokenType[] validTypes) {
            var t = this.tokens.Peek().type;

            foreach (var validType in validTypes) {
                if (t == validType) {
                    return true;
                }
            }
            return false;
        }

        // Returns true if the next symbols are of the same type as
        // 'validTypes' - this is used to look further ahead in the
        // token stream, eg when we're looking for '<<' 'else'
        bool NextSymbolsAre(params TokenType[] validTypes) {
            var tempQueue = new Queue<Token> (tokens);
            foreach (var type in validTypes) {
                if (tempQueue.Dequeue ().type != type)
                    return false;
            }
            return true;
        }

        // Return the next token, which must be of type 'type',
        // or throw an exception
        Token ExpectSymbol(TokenType type) {
            var t = this.tokens.Dequeue();
            if (t.type != type) {

                throw ParseException.Make(t, type);
            }
            return t;
        }

        // Return the next token, which can be of any type except EndOfInput.
        Token ExpectSymbol() {
            var token = this.tokens.Dequeue ();
            if (token.type == TokenType.EndOfInput) {
                throw ParseException.Make (token, "Unexpected end of input");
            }
            return token;
        }

        // Return the next token, which must be one of 'validTypes',
        // or throw an exception
        Token ExpectSymbol(params TokenType[] validTypes) {
            var t = this.tokens.Dequeue();

            foreach (var validType in validTypes) {
                if (t.type == validType) {
                    return t;
                }
            }

            throw ParseException.Make(t, validTypes);
        }
    }

}

