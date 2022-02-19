using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

using Yarn.Compiler;

namespace YarnSpinner.Tests
{


    public class ErrorHandlingTests : TestBase
    {
        [Fact]
        public void TestMalformedIfStatement() {
            var source = CreateTestNode(@"<<if true>> // error: no endif");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            Assert.Collection(result.Diagnostics, d => Assert.Contains("Expected an <<endif>> to match the <<if>> statement on line 3", d.Message));
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

            Assert.Collection(result.Diagnostics, 
                d => Assert.Contains("More than one <<else>> statement in an <<if>> statement isn't allowed", d.Message),
                d => Assert.Contains("Unexpected \"endif\" while reading a statement", d.Message)
            );
        }

        [Fact]
        public void TestEmptyCommand() {
            var source = CreateTestNode(@"
            <<>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));             

            Assert.Collection(result.Diagnostics, 
                d => Assert.Contains("Command text expected", d.Message)
            );
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

                Assert.Collection(result.Diagnostics,
                    d =>
                    {
                        Assert.Contains("Variable names need to start with a $", d.Message);
                        Assert.Equal(3, d.Range.Start.Line);
                    }
                );            
            }
        }

        [Fact]
        public void TestInvalidFunctionCall()
        {
            var source = CreateTestNode("<<if someFunction(>><<endif>>");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            Assert.Collection(result.Diagnostics, d => Assert.Contains(@"Unexpected "">>"" while reading a function call", d.Message));
        }
    }
}
