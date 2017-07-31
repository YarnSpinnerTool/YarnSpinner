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

// Comment out to not catch exceptions
#define CATCH_EXCEPTIONS

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System.Text;
using System.IO;
using System.Linq;

namespace Yarn {

    public enum NodeFormat
    {
        Unknown, // an unknown type

        SingleNodeText, // a plain text file containing a single node with no metadata

        JSON, // a JSON file containing multiple nodes with metadata

        Text, //  a text file containing multiple nodes with metadata

    }

    internal class Loader {

        private Dialogue dialogue;

        public Program program { get; private set; }

        // Prints out the list of tokens that the tokeniser found for this node
        void PrintTokenList(IEnumerable<Token> tokenList) {
            // Sum up the result
            var sb = new System.Text.StringBuilder();
            foreach (var t in tokenList) {
                sb.AppendLine (string.Format("{0} ({1} line {2})", t.ToString (), t.context, t.lineNumber));
            }

            // Let's see what we got
            dialogue.LogDebugMessage("Tokens:");
            dialogue.LogDebugMessage(sb.ToString());

        }

        // Prints the parse tree for the node
        void PrintParseTree(Yarn.Parser.ParseNode rootNode) {
            dialogue.LogDebugMessage("Parse Tree:");
            dialogue.LogDebugMessage(rootNode.PrintTree(0));

        }

        // Prepares a loader. 'implementation' is used for logging.
        public Loader(Dialogue dialogue) {
            if (dialogue == null)
                throw new ArgumentNullException ("dialogue");

            this.dialogue = dialogue;

        }

        // the preprocessor that cleans up things to make it easier on ANTLR
        // replaces \r\n with \n
        // adds in INDENTS and DEDENTS where necessary
        // replaces \t with four spaces
        // takes in a string of yarn and returns a string the compiler can then use
        private struct EmissionTuple
        {
            public int depth;
            public bool emitted;
            public EmissionTuple(int depth, bool emitted)
            {
                this.depth = depth;
                this.emitted = emitted;
            }
        }
        private string preprocessor(string nodeText)
        {
            string processed = null;

            using (StringReader reader = new StringReader(nodeText))
            {
                // a list to hold outputLines once they have been cleaned up
                List<string> outputLines = new List<string>();

				// a stack to keep track of how far indented we are
				// made up of ints and bools
				// ints track the depth, bool tracks if we emitted an indent token
				// starts with 0 and false so we can never fall off the end of the stack
				Stack<EmissionTuple> indents = new Stack<EmissionTuple>();
				indents.Push(new EmissionTuple(0, false));

                // a bool to determine if we are in a mode where we need to track indents
                bool shouldTrackNextIndentation = false;

                char INDENT = '\a';
                char DEDENT = '\v';
                //string INDENT = "{";
                //string DEDENT = "}";

                string OPTION = "->";

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // replacing \t with 4 spaces
                    string tweakedLine = line.Replace("\t", "    ");
                    // stripping of any trailing newlines, will add them back in later
                    tweakedLine = tweakedLine.TrimEnd('\r', '\n');

                    // getting the number of indents on this line
                    int lineIndent = tweakedLine.TakeWhile(Char.IsWhiteSpace).Count();

                    // working out if it is an option (ie does it start with ->)
                    bool isOption = tweakedLine.TrimStart(' ').StartsWith(OPTION);

                    // are we in a state where we need to track indents?
                    var previous = indents.Peek();
                    if (shouldTrackNextIndentation && (lineIndent > previous.depth))
                    {
                        indents.Push(new EmissionTuple(lineIndent, true));
                        // adding an indent to the stream
                        // tries to add it to the end of the previous line where possible
                        if (outputLines.Count == 0)
                        {
                            tweakedLine = INDENT + tweakedLine;
                        }
                        else
                        {
                            outputLines[outputLines.Count - 1] = outputLines[outputLines.Count - 1] + INDENT;
                        }

                        shouldTrackNextIndentation = false;
                    }
                    // have we finished with the current block of statements
                    else if (lineIndent < previous.depth)
                    {
                        while (lineIndent < indents.Peek().depth)
                        {
                            var topLevel = indents.Pop();

                            if (topLevel.emitted)
                            {
                                // adding dedents
								if (outputLines.Count == 0)
								{
                                    tweakedLine = DEDENT + tweakedLine;
								}
								else
								{
                                    outputLines[outputLines.Count - 1] = outputLines[outputLines.Count - 1] + DEDENT;
								}
                            }
                        }
                    }
                    else
                    {
                        shouldTrackNextIndentation = false;
                    }

                    // do we need to track the indents for the next statement?
                    if (isOption)
                    {
                        shouldTrackNextIndentation = true;
                        if (indents.Peek().depth < lineIndent)
                        {
                            indents.Push(new EmissionTuple(lineIndent, false));
                        }
                    }
                    outputLines.Add(tweakedLine);
                }
                // mash it all back together now
                StringBuilder builder = new StringBuilder();
                foreach (string outLine in outputLines)
                {
                    builder.Append(outLine);
                    builder.Append("\n");
                }
                processed = builder.ToString();
            }

            return processed;
        }

        // Given a bunch of raw text, load all nodes that were inside it.
        // You can call this multiple times to append to the collection of nodes,
        // but note that new nodes will replace older ones with the same name.
        // Returns the number of nodes that were loaded.
        public Program Load(string text, Library library, string fileName, Program includeProgram, bool showTokens, bool showParseTree, string onlyConsiderNode, NodeFormat format, bool experimentalMode = false)
        {
			if (format == NodeFormat.Unknown)
			{
				format = GetFormatFromFileName(fileName);
			}

            // currently experimental node can only be used on yarn.txt yarn files and single nodes
            if (experimentalMode && (format == NodeFormat.Text || format == NodeFormat.SingleNodeText))
            {
                // this isn't the greatest...
                if (format == NodeFormat.SingleNodeText)
                {
                    // it is just the body
                    // need to add a dummy header and body delimiters
                    StringBuilder builder = new StringBuilder();
                    builder.Append("title:Start\n");
                    builder.Append("---\n");
                    builder.Append(text);
                    builder.Append("\n===\n");
                    text = builder.ToString();
                }

                string inputString = preprocessor(text);
                ICharStream input = CharStreams.fromstring(inputString);

                YarnSpinnerLexer lexer = new YarnSpinnerLexer(input);
                CommonTokenStream tokens = new CommonTokenStream(lexer);

                YarnSpinnerParser parser = new YarnSpinnerParser(tokens);
                // turning off the normal error listener and using ours
                parser.RemoveErrorListeners();
                parser.AddErrorListener(ErrorListener.Instance);

                IParseTree tree = parser.dialogue();
                AntlrCompiler antlrcompiler = new AntlrCompiler(library);
                antlrcompiler.Compile(tree);

                // merging in the other program if requested
                if (includeProgram != null)
                {
                    antlrcompiler.program.Include(includeProgram);
                }

                return antlrcompiler.program;
            }
            else
            {
                // The final parsed nodes that were in the file we were given
                Dictionary<string, Yarn.Parser.Node> nodes = new Dictionary<string, Parser.Node>();

                // Load the raw data and get the array of node title-text pairs

                var nodeInfos = GetNodesFromText(text, format);

                int nodesLoaded = 0;

                foreach (NodeInfo nodeInfo in nodeInfos)
                {

                    if (onlyConsiderNode != null && nodeInfo.title != onlyConsiderNode)
                        continue;

                    // Attempt to parse every node; log if we encounter any errors
#if CATCH_EXCEPTIONS
                    try
                    {
#endif

                        if (nodes.ContainsKey(nodeInfo.title))
                        {
                            throw new InvalidOperationException("Attempted to load a node called " +
                                nodeInfo.title + ", but a node with that name has already been loaded!");
                        }

                        var lexer = new Lexer();
                        var tokens = lexer.Tokenise(nodeInfo.title, nodeInfo.body);

                        if (showTokens)
                            PrintTokenList(tokens);

                        var node = new Parser(tokens, library).Parse();

                        // If this node is tagged "rawText", then preserve its source
                        if (string.IsNullOrEmpty(nodeInfo.tags) == false &&
                            nodeInfo.tags.Contains("rawText"))
                        {
                            node.source = nodeInfo.body;
                        }

                        node.name = nodeInfo.title;

                        node.nodeTags = nodeInfo.tagsList;

                        if (showParseTree)
                            PrintParseTree(node);

                        nodes[nodeInfo.title] = node;

                        nodesLoaded++;

#if CATCH_EXCEPTIONS
                    }
                    catch (Yarn.TokeniserException t)
                    {
                        // Add file information
                        var message = string.Format("In file {0}: Error reading node {1}: {2}", fileName, nodeInfo.title, t.Message);
                        throw new Yarn.TokeniserException(message);
                    }
                    catch (Yarn.ParseException p)
                    {
                        var message = string.Format("In file {0}: Error parsing node {1}: {2}", fileName, nodeInfo.title, p.Message);
                        throw new Yarn.ParseException(message);
                    }
                    catch (InvalidOperationException e)
                    {
                        var message = string.Format("In file {0}: Error reading node {1}: {2}", fileName, nodeInfo.title, e.Message);
                        throw new InvalidOperationException(message);
                    }
#endif
                }

                var compiler = new Yarn.Compiler(fileName);

                foreach (var node in nodes)
                {
                    compiler.CompileNode(node.Value);
                }

                if (includeProgram != null)
                {
                    compiler.program.Include(includeProgram);
                }

                return compiler.program;
            }
        }

        // The raw text of the Yarn node, plus metadata
        // All properties are serialised except tagsList, which is a derived property
        [JsonObject(MemberSerialization.OptOut)]
        public struct NodeInfo {
            public struct Position {
                public int x { get; set; }
                public int y { get; set; }
            }

            public string title { get; set; }
            public string body { get; set; }

            // The raw "tags" field, containing space-separated tags. This is written
            // to the file.
            public string tags { get; set; }

            public int colorID { get; set; }
            public Position position { get; set; }

            // The tags for this node, as a list of individual strings.
            [JsonIgnore]
            public List<string> tagsList
            {
                get
                {
                    // If we have no tags list, or it's empty, return the empty list
                    if (tags == null || tags.Length == 0) {
                        return new List<string>();
                    }

                    return new List<string>(tags.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                }
            }

        }

        internal static NodeFormat GetFormatFromFileName(string fileName)
        {
            NodeFormat format;
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                format = NodeFormat.JSON;
            }
            else if (fileName.EndsWith(".yarn.txt", StringComparison.OrdinalIgnoreCase))
            {
                format = NodeFormat.Text;
            }
            else if (fileName.EndsWith(".node", StringComparison.OrdinalIgnoreCase))
            {
                format = NodeFormat.SingleNodeText;
            }
            else {
                throw new FormatException(string.Format("Unknown file format for file '{0}'", fileName));
            }

            return format;
        }

        // Given either Twine, JSON or XML input, return an array
        // containing info about the nodes in that file
        internal NodeInfo[] GetNodesFromText(string text, NodeFormat format)
        {
            // All the nodes we found in this file
            var nodes = new List<NodeInfo> ();

            switch (format)
            {
                case NodeFormat.SingleNodeText:
                    // If it starts with a comment, treat it as a single-node file
                    var nodeInfo = new NodeInfo();
                    nodeInfo.title = "Start";
                    nodeInfo.body = text;
                    nodes.Add(nodeInfo);
                    break;
                case NodeFormat.JSON:
                    // Parse it as JSON
                    try
                    {
                        nodes = JsonConvert.DeserializeObject<List<NodeInfo>>(text);
                    }
                    catch (JsonReaderException e)
                    {
                        dialogue.LogErrorMessage("Error parsing Yarn input: " + e.Message);
                    }

                    break;
                case NodeFormat.Text:

                    // check for the existence of at least one "---"+newline sentinel, which divides
                    // the headers from the body

                    // we use a regex to match either \r\n or \n line endings
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, "---.?\n") == false) {
                        dialogue.LogErrorMessage("Error parsing input: text appears corrupt (no header sentinel)");
                        break;
                    }

                    var headerRegex = new System.Text.RegularExpressions.Regex("(?<field>.*): *(?<value>.*)");

                    var nodeProperties = typeof(NodeInfo).GetProperties();

                    int lineNumber = 0;

                    using (var reader = new System.IO.StringReader(text))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {

                            // Create a new node
                            NodeInfo node = new NodeInfo();

                            // Read header lines
                            do
                            {
                                lineNumber++;

                                // skip empty lines
                                if (line.Length == 0)
                                {
                                    continue;
                                }

                                // Attempt to parse the header
                                var headerMatches = headerRegex.Match(line);

                                if (headerMatches == null)
                                {
                                    dialogue.LogErrorMessage(string.Format("Line {0}: Can't parse header '{1}'", lineNumber, line));
                                    continue;
                                }

                                var field = headerMatches.Groups["field"].Value;
                                var value = headerMatches.Groups["value"].Value;

                                // Attempt to set the appropriate property using this field
                                foreach (var property in nodeProperties)
                                {
                                    if (property.Name != field) {
                                        continue;
                                    }

                                    // skip properties that can't be written to
                                    if (property.CanWrite == false)
                                    {
                                        continue;
                                    }
                                    try
                                    {
                                        var propertyType = property.PropertyType;
                                        object convertedValue;
                                        if (propertyType.IsAssignableFrom(typeof(string)))
                                        {
                                            convertedValue = value;
                                        }
                                        else if (propertyType.IsAssignableFrom(typeof(int)))
                                        {
                                            convertedValue = int.Parse(value);
                                        }
                                        else if (propertyType.IsAssignableFrom(typeof(NodeInfo.Position)))
                                        {
                                            var components = value.Split(',');

                                            // we expect 2 components: x and y
                                            if (components.Length != 2)
                                            {
                                                throw new FormatException();
                                            }

                                            var position = new NodeInfo.Position();
                                            position.x = int.Parse(components[0]);
                                            position.y = int.Parse(components[1]);

                                            convertedValue = position;
                                        }
                                        else {
                                            throw new NotSupportedException();
                                        }
                                        // we need to box this because structs are value types,
                                        // so calling SetValue using 'node' would just modify a copy of 'node'
                                        object box = node;
                                        property.SetValue(box, convertedValue, null);
                                        node = (NodeInfo)box;
                                        break;
                                    }
                                    catch (FormatException)
                                    {
                                        dialogue.LogErrorMessage(string.Format("{0}: Error setting '{1}': invalid value '{2}'", lineNumber, field, value));
                                    }
                                    catch (NotSupportedException)
                                    {
                                        dialogue.LogErrorMessage(string.Format("{0}: Error setting '{1}': This property cannot be set", lineNumber, field));
                                    }
                                }
                            } while ((line = reader.ReadLine()) != "---");

                            lineNumber++;

                            // We're past the header; read the body

                            var lines = new List<string>();

                            // Read header lines until we hit the end of node sentinel or the end of the file
                            while ((line = reader.ReadLine()) != "===" && line != null)
                            {
                                lineNumber++;
                                lines.Add(line);
                            }
                            // We're done reading the lines! Zip 'em up into a string and
                            // store it in the body
                            node.body = string.Join("\n", lines.ToArray());

                            // And add this node to the list
                            nodes.Add(node);

                            // And now we're ready to move on to the next line!

                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unknown format " + format.ToString());
            }

            // hooray we're done
            return nodes.ToArray();
        }

    }

}