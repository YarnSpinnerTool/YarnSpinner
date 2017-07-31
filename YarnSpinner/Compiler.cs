using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Yarn
{
    internal class Compiler
    {
        struct CompileFlags {
            // should we emit code that turns (VAR_SHUFFLE_OPTIONS) off
            // after the next RunOptions bytecode?
            public bool DisableShuffleOptionsAfterNextSet;
        }

        CompileFlags flags;

        internal Program program { get; private set; }

        internal Compiler (string programName)
        {
            program = new Program ();
        }

        internal void CompileNode(Parser.Node node) {

            if (program.nodes.ContainsKey(node.name)) {
                throw new ArgumentException ("Duplicate node name " + node.name);
            }

            var compiledNode =  new Node();

            compiledNode.name = node.name;

            compiledNode.tags = node.nodeTags;

            // Register the entire text of this node if we have it
            if (node.source != null)
            {
                // Dump the entire contents of this node into the string table
                // instead of compiling its contents.

                // the line number is 0 because the string starts at the start of the node
                compiledNode.sourceTextStringID = program.RegisterString(node.source, node.name, "line:"+node.name, 0, true);
            } else {

                // Compile the node.

                var startLabel = RegisterLabel();
                Emit(compiledNode, ByteCode.Label, startLabel);

                foreach (var statement in node.statements)
                {
                    GenerateCode(compiledNode, statement);
                }

                // Does this node end after emitting AddOptions codes
                // without calling ShowOptions?

                // Note: this only works when we know that we don't have
                // AddOptions and then Jump up back into the code to run them.
                // TODO: A better solution would be for the parser to flag
                // whether a node has Options at the end.
                var hasRemainingOptions = false;
                foreach (var instruction in compiledNode.instructions)
                {
                    if (instruction.operation == ByteCode.AddOption)
                    {
                        hasRemainingOptions = true;
                    }
                    if (instruction.operation == ByteCode.ShowOptions)
                    {
                        hasRemainingOptions = false;
                    }
                }

                // If this compiled node has no lingering options to show at the end of the node, then stop at the end
                if (hasRemainingOptions == false)
                {
                    Emit(compiledNode, ByteCode.Stop);
                }
                else {
                    // Otherwise, show the accumulated nodes and then jump to the selected node

                    Emit(compiledNode, ByteCode.ShowOptions);

                    if (flags.DisableShuffleOptionsAfterNextSet == true)
                    {
                        Emit(compiledNode, ByteCode.PushBool, false);
                        Emit(compiledNode, ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
                        Emit(compiledNode, ByteCode.Pop);
                        flags.DisableShuffleOptionsAfterNextSet = false;
                    }

                    Emit(compiledNode, ByteCode.RunNode);
                }

            }

            program.nodes[compiledNode.name] = compiledNode;

        }

        private int labelCount = 0;

        // Generates a unique label name to use
        string RegisterLabel(string commentary = null) {
            return "L" + labelCount++ + commentary;
        }

        void Emit(Node node, ByteCode code, object operandA = null, object operandB = null) {
            var instruction = new Instruction();
            instruction.operation = code;
            instruction.operandA = operandA;
            instruction.operandB = operandB;

            node.instructions.Add (instruction);

            if (code == ByteCode.Label) {
                // Add this label to the label table
                node.labels.Add ((string)instruction.operandA, node.instructions.Count - 1);
            }
        }

        // Statements
        void GenerateCode(Node node, Parser.Statement statement) {
            switch (statement.type) {
            case Parser.Statement.Type.CustomCommand:
                GenerateCode (node, statement.customCommand);
                break;
            case Parser.Statement.Type.ShortcutOptionGroup:
                GenerateCode (node, statement.shortcutOptionGroup);
                break;
            case Parser.Statement.Type.Block:

                // Blocks are just groups of statements
                foreach (var blockStatement in statement.block.statements) {
                    GenerateCode(node, blockStatement);
                }

                break;

            case Parser.Statement.Type.IfStatement:
                GenerateCode (node, statement.ifStatement);
                break;

            case Parser.Statement.Type.OptionStatement:
                GenerateCode (node, statement.optionStatement);
                break;

            case Parser.Statement.Type.AssignmentStatement:
                GenerateCode (node, statement.assignmentStatement);
                break;

            case Parser.Statement.Type.Line:
                GenerateCode (node, statement, statement.line);
                break;

            default:
                throw new ArgumentOutOfRangeException ();
            }

        }

        void GenerateCode(Node node, Parser.CustomCommand statement) {

            // If this command is an evaluable expression, evaluate it
            if (statement.expression != null) {
                GenerateCode (node, statement.expression);
            } else {
                switch (statement.clientCommand) {
                case "stop":
                    Emit (node, ByteCode.Stop);
                    break;
                case "shuffleNextOptions":
                    // Emit code that sets "VAR_SHUFFLE_OPTIONS" to true
                    Emit (node, ByteCode.PushBool, true);
                    Emit (node, ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
                    Emit (node, ByteCode.Pop);
                    flags.DisableShuffleOptionsAfterNextSet = true;
                    break;

                default:
                    Emit (node, ByteCode.RunCommand, statement.clientCommand);
                    break;
                }
            }

        }

        string GetLineIDFromNodeTags(Parser.ParseNode node) {
            // TODO: This will use only the first #line: tag, ignoring all others
            foreach (var tag in node.tags)
            {
                if (tag.StartsWith("line:"))
                {
                    return tag;
                }
            }
            return null;
        }

        void GenerateCode(Node node, Parser.Statement parseNode, string line) {

            // Does this line have a "#line:LINENUM" tag? Use it
            string lineID = GetLineIDFromNodeTags(parseNode);

            var num = program.RegisterString (line, node.name, lineID, parseNode.lineNumber, true);

            Emit (node, ByteCode.RunLine, num);

        }

        void GenerateCode(Node node, Parser.ShortcutOptionGroup statement) {

            var endOfGroupLabel = RegisterLabel ("group_end");

            var labels = new List<string> ();

            int optionCount = 0;
            foreach (var shortcutOption in statement.options) {

                var optionDestinationLabel = RegisterLabel ("option_" + (optionCount+1));
                labels.Add (optionDestinationLabel);

                string endOfClauseLabel = null;

                if (shortcutOption.condition != null) {
                    endOfClauseLabel = RegisterLabel ("conditional_"+optionCount);
                    GenerateCode (node, shortcutOption.condition);

                    Emit (node, ByteCode.JumpIfFalse, endOfClauseLabel);
                }

                var labelLineID = GetLineIDFromNodeTags(shortcutOption);

                var labelStringID = program.RegisterString (shortcutOption.label, node.name, labelLineID, shortcutOption.lineNumber, true);

                Emit (node, ByteCode.AddOption, labelStringID, optionDestinationLabel);

                if (shortcutOption.condition != null) {
                    Emit (node, ByteCode.Label, endOfClauseLabel);
                    Emit (node, ByteCode.Pop);
                }

                optionCount++;
            }

            Emit (node, ByteCode.ShowOptions);

            if (flags.DisableShuffleOptionsAfterNextSet == true) {
                Emit (node, ByteCode.PushBool, false);
                Emit (node, ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
                Emit (node, ByteCode.Pop);
                flags.DisableShuffleOptionsAfterNextSet = false;
            }

            Emit (node, ByteCode.Jump);

            optionCount = 0;
            foreach (var shortcutOption in statement.options) {

                Emit (node, ByteCode.Label, labels [optionCount]);

                if (shortcutOption.optionNode != null)
                    GenerateCode (node, shortcutOption.optionNode.statements);

                Emit (node, ByteCode.JumpTo, endOfGroupLabel);

                optionCount++;

            }

            // reached the end of the option group
            Emit (node, ByteCode.Label, endOfGroupLabel);

            // clean up after the jump
            Emit (node, ByteCode.Pop);

        }

        void GenerateCode(Node node, IEnumerable<Yarn.Parser.Statement> statementList) {

            if (statementList == null)
                return;

            foreach (var statement in statementList) {
                GenerateCode (node, statement);
            }
        }

        void GenerateCode(Node node, Parser.IfStatement statement) {

            // We'll jump to this label at the end of every clause
            var endOfIfStatementLabel = RegisterLabel ("endif");

            foreach (var clause in statement.clauses) {
                var endOfClauseLabel = RegisterLabel ("skipclause");

                if (clause.expression != null) {

                    GenerateCode (node, clause.expression);

                    Emit (node, ByteCode.JumpIfFalse, endOfClauseLabel);

                }

                GenerateCode (node, clause.statements);

                Emit (node, ByteCode.JumpTo, endOfIfStatementLabel);

                if (clause.expression != null) {
                    Emit (node, ByteCode.Label, endOfClauseLabel);
                }
                // Clean up the stack by popping the expression that was tested earlier
                if (clause.expression != null) {
                    Emit (node, ByteCode.Pop);
                }
            }

            Emit (node, ByteCode.Label, endOfIfStatementLabel);
        }

        void GenerateCode(Node node, Parser.OptionStatement statement) {

            var destination = statement.destination;

            if (statement.label == null) {
                // this is a jump to another node
                Emit(node, ByteCode.RunNode, destination);
            } else {

                var lineID = GetLineIDFromNodeTags(statement.parent);

                var stringID = program.RegisterString (statement.label, node.name, lineID, statement.lineNumber, true);

                Emit (node, ByteCode.AddOption, stringID, destination);
            }

        }

        void GenerateCode(Node node, Parser.AssignmentStatement statement) {

            // Is it a straight assignment?
            if (statement.operation == TokenType.EqualToOrAssign) {
                // Evaluate the expression, which will result in a value
                // on the stack
                GenerateCode (node, statement.valueExpression);

                // Stack now contains [destinationValue]
            } else {

                // It's a combined operation-plus-assignment

                // Get the current value of the variable
                Emit(node, ByteCode.PushVariable, statement.destinationVariableName);

                // Evaluate the expression, which will result in a value
                // on the stack
                GenerateCode (node, statement.valueExpression);

                // Stack now contains [currentValue, expressionValue]

                switch (statement.operation) {

                case TokenType.AddAssign:
                    Emit (node, ByteCode.CallFunc, TokenType.Add.ToString ());
                    break;
                case TokenType.MinusAssign:
                    Emit (node, ByteCode.CallFunc, TokenType.Minus.ToString ());
                    break;
                case TokenType.MultiplyAssign:
                    Emit (node, ByteCode.CallFunc, TokenType.Multiply.ToString ());
                    break;
                case TokenType.DivideAssign:
                    Emit (node, ByteCode.CallFunc, TokenType.Divide.ToString ());
                    break;
                default:
                    throw new ArgumentOutOfRangeException ();
                }

                // Stack now contains [destinationValue]
            }

            // Store the top of the stack in the variable
            Emit(node, ByteCode.StoreVariable, statement.destinationVariableName);

            // Clean up the stack
            Emit (node, ByteCode.Pop);

        }

        void GenerateCode(Node node, Parser.Expression expression) {

            // Expressions are either plain values, or function calls
            switch (expression.type) {
            case Parser.Expression.Type.Value:
                // Plain value? Emit that
                GenerateCode (node, expression.value);
                break;
            case Parser.Expression.Type.FunctionCall:
                // Evaluate all parameter expressions (which will
                // push them to the stack)
                foreach (var parameter in expression.parameters) {
                    GenerateCode (node, parameter);
                }
                // If this function has a variable number of parameters, put
                // the number of parameters that were passed onto the stack
                if (expression.function.paramCount == -1) {
                    Emit (node, ByteCode.PushNumber, expression.parameters.Count);
                }

                // And then call the function
                Emit (node, ByteCode.CallFunc, expression.function.name);
                break;
            }
        }

        void GenerateCode(Node node, Parser.ValueNode value) {

            // Push a value onto the stack

            switch (value.value.type) {
            case Value.Type.Number:
                Emit (node, ByteCode.PushNumber, value.value.numberValue);
                break;
            case Value.Type.String:
                // TODO: we use 'null' as the line ID here because strings used in expressions
                // don't have a #line: tag we can use
                var id = program.RegisterString (value.value.stringValue, node.name, null, value.lineNumber, false);
                Emit (node, ByteCode.PushString, id);
                break;
            case Value.Type.Bool:
                Emit (node, ByteCode.PushBool, value.value.boolValue);
                break;
            case Value.Type.Variable:
                Emit (node, ByteCode.PushVariable, value.value.variableName);
                break;
            case Value.Type.Null:
                Emit (node, ByteCode.PushNull);
                break;
            default:
                throw new ArgumentOutOfRangeException ();
            }
        }

    }
}

