using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;
using FluentAssertions;

using Yarn.Compiler;

namespace YarnSpinner.Tests
{


    public class ErrorHandlingTests : TestBase
    {
        [Fact]
        public void TestMalformedIfStatement() {
            var source = CreateTestNode(@"<<if true>> // error: no endif");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains("Expected an <<endif>> to match the <<if>> statement on line 3"));
        }

        [Fact]
        public void TestExtraneousElse() {
            var source = CreateTestNode(@"
            <<if true>>
            One
            <<else>>
            Two
            <<else>>
            Three
            <<endif>>");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains("More than one <<else>> statement in an <<if>> statement isn't allowed"));
            result.Diagnostics.Should().Contain(d => d.Message.Contains("Unexpected \"endif\" while reading a statement"));
            
        }

        [Fact]
        public void TestEmptyCommand() {
            var source = CreateTestNode(@"
            <<>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains("Command text expected"));
        }

        [Fact]
        public void TestInvalidVariableNameInSetOrDeclare() {
            var source1 = CreateTestNode(@"
            <<set test = 1>>
            ");

            var source2 = CreateTestNode(@"
            <<declare test = 1>>
            ");
            
            foreach (var source in new[] { source1, source2}) {

                var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

                result.Diagnostics.Should().Contain(d => d.Message == "Variable names need to start with a $");
            }
        }

        [Fact]
        public void TestInvalidFunctionCall()
        {
            var source = CreateTestNode("<<if someFunction(>><<endif>>");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains(@"Unexpected "">>"" while reading a function call"));
        }

        // testing that warnings are generated for empty nodes
        [Fact]
        public void TestEmptyNodesGenerateWarnings()
        {
            var source = CreateTestNode("", "Start");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<inputs>", source));

            var warnings = result.Diagnostics.Where(d => d.Severity == Diagnostic.DiagnosticSeverity.Warning);
            // there should be only one warning
            warnings.Count().Should().Be(1);
            // and it should be the warning about empty nodes
            warnings.FirstOrDefault().Message.Should().Be("Node \"Start\" is empty and will not be included in the compiled output.");
        }
    }
}
