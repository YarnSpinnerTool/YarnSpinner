using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

using Yarn.Compiler;
using CLDRPlurals;

namespace YarnSpinner.Tests
{


    public class TypeTests : TestBase
    {
        public TypeTests() : base()
        {
        }

        [Fact]
        void TestVariableDeclarationsParsed()
        {
            var source = CreateTestNode(@"
            <<declare $int = 5>>            
            <<declare $str = ""yes"">>
            
            // These value changes are allowed, 
            // because they match the declared type
            <<set $int = 6>>
            <<set $str = ""no"">>
            <<set $bool = false>>

            // Declarations are allowed anywhere in the program
            <<declare $bool = true>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            Assert.Empty(result.Diagnostics);

            var expectedDeclarations = new List<Declaration>() {
                new Declaration {
                    Name = "$int",
                    Type = BuiltinTypes.Number,
                    DefaultValue = 5f,
                    Range = new Yarn.Compiler.Range {
                        Start = {
                            Line = 3,
                            Character = 22,
                        },
                        End = {
                            Line = 3,
                            Character = 26,
                        }
                    },
                    SourceNodeName = "Start",
                    SourceFileName = "input",
                },
                new Declaration {
                    Name = "$str",
                    Type = BuiltinTypes.String,
                    DefaultValue = "yes",
                    Range = new Yarn.Compiler.Range {
                        Start = {
                            Line = 4,
                            Character = 22,
                        },
                        End = {
                            Line = 4,
                            Character = 26,
                        }
                    },
                    SourceNodeName = "Start",
                    SourceFileName = "input",
                },
                new Declaration {
                    Name = "$bool",
                    Type = BuiltinTypes.Boolean,
                    DefaultValue = true,
                    Range = new Yarn.Compiler.Range {
                        Start = {
                            Line = 13,
                            Character = 22,
                        },
                        End = {
                            Line = 13,
                            Character = 27,
                        }
                    },
                    SourceNodeName = "Start",
                    SourceFileName = "input",
                },
            };

            var actualDeclarations = new List<Declaration>(result.Declarations);

            for (int i = 0; i < expectedDeclarations.Count; i++)
            {
                Declaration expected = expectedDeclarations[i];
                Declaration actual = actualDeclarations[i];

                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.Type, actual.Type);
                Assert.Equal(expected.DefaultValue, actual.DefaultValue);
                Assert.Equal(expected.Range, actual.Range);
                Assert.Equal(expected.SourceNodeName, actual.SourceNodeName);
                Assert.Equal(expected.SourceFileName, actual.SourceFileName);
            }
        }

        [Fact]
        public void TestDeclarationsCanAppearInOtherFiles()
        {
            // Create two separately-compiled compilation units that each
            // declare a variable that's modified by the other
            var sourceA = CreateTestNode(@"
            <<declare $varB = 1>>
            <<set $varA = 2>>
            ", "NodeA");

            var sourceB = CreateTestNode(@"
            <<declare $varA = 1>>
            <<set $varB = 2>>
            ", "NodeB");

            var compilationJob = new CompilationJob
            {
                Files = new[] {
                    new CompilationJob.File { FileName = "sourceA", Source = sourceA  },
                    new CompilationJob.File { FileName = "sourceB", Source = sourceB  },
                },
            };

            var result = Compiler.Compile(compilationJob);

            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void TestImportingVariableDeclarations()
        {
            var source = CreateTestNode(@"
            <<set $int = 6>> // no error; declaration is imported            
            ");

            var declarations = new[] {
                new Declaration {
                    Name = "$int",
                    Type = BuiltinTypes.Number,
                    DefaultValue = 0,
                }
            };

            CompilationJob compilationJob = CompilationJob.CreateFromString("input", source);

            // Provide the declarations
            compilationJob.VariableDeclarations = declarations;

            // Should compile with no errors because $int was declared
            var result = Compiler.Compile(compilationJob);

            Assert.Empty(result.Diagnostics);

            // No variables are declared in the source code, so we should
            // expect an empty collection of variable declarations
            Assert.Empty(result.Declarations);
        }

        [Fact]
        void TestVariableDeclarationsDisallowDuplicates()
        {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<declare $int = 6>> // error! redeclaration of $int        
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            Assert.Collection(result.Diagnostics, p => Assert.Contains("$int has already been declared", p.Message));
        }

        [Fact]
        public void TestExpressionsDisallowMismatchedTypes()
        {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<set $int = ""5"">> // error, can't assign string to a variable declared int
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            
            Assert.Collection(result.Diagnostics, p => Assert.Contains("$int (Number) cannot be assigned a String", p.Message));
        }

        [Theory]
        [InlineData(@"<<set $str = ""hi"">>")] // in commands
        [InlineData(@"{$str + 1}")] // in inline expressions
        public void TestExpressionsAllowsUsingUndeclaredVariables(string testSource)
        {
            var source = CreateTestNode($@"
            {testSource}
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            
            Assert.Empty(result.Diagnostics);         
        }

        [Theory]
        [CombinatorialData]
        public void TestExpressionsRequireCompatibleTypes(bool declare)
        {
            var source = CreateTestNode($@"
            {(declare ? "<<declare $int = 0>>" : "")}
            {(declare ? "<<declare $bool = false>>" : "")}
            {(declare ? "<<declare $str = \"\">>" : "")}

            <<set $int = 1>>
            <<set $int = 1 + 1>>
            <<set $int = 1 - 1>>
            <<set $int = 1 * 2>>
            <<set $int = 1 / 2>>
            <<set $int = 1 % 2>>
            <<set $int += 1>>
            <<set $int -= 1>>
            <<set $int *= 1>>
            <<set $int /= 1>>
            <<set $int %= 1>>

            <<set $str = ""hello"">>
            <<set $str = ""hel"" + ""lo"">>

            <<set $bool = true>>
            <<set $bool = 1 > 1>>
            <<set $bool = 1 < 1>>
            <<set $bool = 1 <= 1>>
            <<set $bool = 1 >= 1>>

            <<set $bool = ""hello"" == ""hello"">>
            <<set $bool = ""hello"" != ""goodbye"">>
            <<set $bool = 1 == 1>>
            <<set $bool = 1 != 2>>
            <<set $bool = true == true>>
            <<set $bool = true != false>>

            <<set $bool = (1 + 1) > 2>>
            ");

            // Should compile with no exceptions
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void TestNullNotAllowed()
        {
            var source = CreateTestNode(@"
            <<declare $err = null>> // error, null not allowed
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            
            Assert.Collection(result.Diagnostics, p => Assert.Contains("Null is not a permitted type", p.Message));
        }

        [Theory]
        [InlineData("<<set $bool = func_void_bool()>>")]
        [InlineData("<<set $bool = func_int_bool(1)>>")]
        [InlineData("<<set $bool = func_int_int_bool(1, 2)>>")]
        [InlineData(@"<<set $bool = func_string_string_bool(""1"", ""2"")>>")]
        public void TestFunctionSignatures(string source)
        {
            dialogue.Library.RegisterFunction("func_void_bool", () => true);
            dialogue.Library.RegisterFunction("func_int_bool", (int i) => true);
            dialogue.Library.RegisterFunction("func_int_int_bool", (int i, int j) => true);
            dialogue.Library.RegisterFunction("func_string_string_bool", (string i, string j) => true);

            var correctSource = CreateTestNode(source);

            // Should compile with no exceptions
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", correctSource, dialogue.Library));

            Assert.Empty(result.Diagnostics);

            // The variable '$bool' should have an implicit declaration.
            var variableDeclarations = result.Declarations.Where(d => d.Name == "$bool");

            Assert.Single(variableDeclarations);

            var variableDeclaration = variableDeclarations.First();

            // The type of the variable should be Boolean, because that's
            // the return type of all of the functions we declared.
            Assert.Same(BuiltinTypes.Boolean, variableDeclaration.Type);

        }

        [Theory, CombinatorialData]
        public void TestOperatorsAreTypeChecked([CombinatorialValues(
            "= 1 + 1",
            "= 1 / 1",
            "= 1 - 1",
            "= 1 * 1",
            "= 1 % 1",
            "+= 1",
            "-= 1",
            "/= 1",
            "*= 1"
            )] string operation, bool declared)
        {

            string source = CreateTestNode($@"
                {(declared ? "<<declare $var = 0>>" : "")}
                <<set $var {operation}>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void TestFailingFunctionDeclarationReturnType() {
            
            dialogue.Library.RegisterFunction("func_invalid_return", () => new List<int> { 1, 2, 3 });
            
            var source = CreateTestNode(@"Hello");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            Assert.Collection(result.Diagnostics, p => Assert.Contains("not a valid return type", p.Message));

        }

        [Fact]
        public void TestFailingFunctionDeclarationParameterType()
        {
            dialogue.Library.RegisterFunction("func_invalid_param", (List<int> listOfInts) => true);

            var source = CreateTestNode(@"Hello");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            Assert.Collection(result.Diagnostics, p => Assert.Contains("parameter listOfInts's type (System.Collections.Generic.List`1[System.Int32]) cannot be used in Yarn functions", p.Message));

        }

        [Theory]
        [InlineData("<<set $bool = func_void_bool(1)>>", "expects 0 parameters, but received 1")]
        [InlineData("<<set $bool = func_int_bool()>>", "expects 1 parameter, but received 0")]
        [InlineData("<<set $bool = func_int_bool(true)>>", "expects a Number, not a Bool")]
        [InlineData(@"<<set $bool = func_string_string_bool(""1"", 2)>>", "expects a String, not a Number")]
        [InlineData("<<set $int = func_void_bool()>>", @"\$int \(Number\) cannot be assigned a Bool")]
        public void TestFailingFunctionSignatures(string source, string expectedExceptionMessage)
        {
            dialogue.Library.RegisterFunction("func_void_bool", () => true);
            dialogue.Library.RegisterFunction("func_int_bool", (int i) => true);
            dialogue.Library.RegisterFunction("func_int_int_bool", (int i, int j) => true);
            dialogue.Library.RegisterFunction("func_string_string_bool", (string i, string j) => true);
            
            var failingSource = CreateTestNode($@"
                <<declare $bool = false>>
                <<declare $int = 1>>
                {source}
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", failingSource, dialogue.Library));

            Assert.Collection(result.Diagnostics, p => Assert.Matches(expectedExceptionMessage, p.Message));
        }

        [Fact]
        public void TestInitialValues()
        {
            var source = CreateTestNode(@"
            <<declare $int = 42>>
            <<declare $str = ""Hello"">>
            <<declare $bool = true>>
            // internal decls
            {$int}
            {$str}
            {$bool}
            // external decls
            {$external_int}
            {$external_str}
            {$external_bool}
            ");

            testPlan = new TestPlanBuilder()
                // internal decls
                .AddLine("42")
                .AddLine("Hello")
                .AddLine("True")
                // external decls
                .AddLine("42")
                .AddLine("Hello")
                .AddLine("True")
                .GetPlan();

            CompilationJob compilationJob = CompilationJob.CreateFromString("input", source, dialogue.Library);

            compilationJob.VariableDeclarations = new[] {
                new Declaration {
                    Name = "$external_str",
                    Type = BuiltinTypes.String,
                    DefaultValue = "Hello",
                },
                new Declaration {
                    Name = "$external_int",
                    Type = BuiltinTypes.Boolean,
                    DefaultValue = true,
                },
                new Declaration {
                    Name = "$external_bool",
                    Type = BuiltinTypes.Number,
                    DefaultValue = 42,
                },
            };

            var result = Compiler.Compile(compilationJob);

            Assert.Empty(result.Diagnostics);

            this.storage.SetValue("$external_str", "Hello");
            this.storage.SetValue("$external_int", 42);
            this.storage.SetValue("$external_bool", true);

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            RunStandardTestcase();

        }

        [Fact]
        public void TestExplicitTypes()
        {
            var source = CreateTestNode(@"
            <<declare $str = ""hello"" as string>>
            <<declare $int = 1 as number>>
            <<declare $bool = false as bool>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            Assert.Empty(result.Diagnostics);

            Assert.Collection(
                result.Declarations.Where(d => d.Name.StartsWith("$")),
                d => { Assert.Equal(d.Name, "$str");  Assert.Equal(d.Type.Name, "String"); },
                d => { Assert.Equal(d.Name, "$int");  Assert.Equal(d.Type.Name, "Number"); },
                d => { Assert.Equal(d.Name, "$bool"); Assert.Equal(d.Type.Name, "Bool"); }
            );
        }

        


        [Theory]
        [InlineData(@"<<declare $str = ""hello"" as number>>")]
        [InlineData(@"<<declare $int = 1 as bool>>")]
        [InlineData(@"<<declare $bool = false as string>>")]
        public void TestExplicitTypesMustMatchValue(string test)
        {
            var source = CreateTestNode(test);

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            Assert.Collection(result.Diagnostics, p => Assert.Matches(@"Type \w+ does not match", p.Message));
        }

        [Fact]
        public void TestVariableDeclarationAnnotations()
        {
            var source = CreateTestNode(@"
            /// prefix: a number
            <<declare $prefix_int = 42>>

            /// prefix: a string
            <<declare $prefix_str = ""Hello"">>

            /// prefix: a bool
            <<declare $prefix_bool = true>>

            <<declare $suffix_int = 42>> /// suffix: a number

            <<declare $suffix_str = ""Hello"">> /// suffix: a string

            <<declare $suffix_bool = true>> /// suffix: a bool
            
            // No declaration before
            <<declare $none_int = 42>> // No declaration after

            /// Multi-line
            /// doc comment
            <<declare $multiline = 42>>

            ");
            
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            Assert.Empty(result.Diagnostics);

            var expectedDeclarations = new List<Declaration>() {
                new Declaration {
                    Name = "$prefix_int",
                    Type = BuiltinTypes.Number,
                    DefaultValue = 42f,
                    Description = "prefix: a number",
                },
                new Declaration {
                    Name = "$prefix_str",
                    Type = BuiltinTypes.String,
                    DefaultValue = "Hello",
                    Description = "prefix: a string",
                },
                new Declaration {
                    Name = "$prefix_bool",
                    Type = BuiltinTypes.Boolean,
                    DefaultValue = true,
                    Description = "prefix: a bool",
                },
                new Declaration {
                    Name = "$suffix_int",
                    Type = BuiltinTypes.Number,
                    DefaultValue = 42f,
                    Description = "suffix: a number",
                },
                new Declaration {
                    Name = "$suffix_str",
                    Type = BuiltinTypes.String,
                    DefaultValue = "Hello",
                    Description = "suffix: a string",
                },
                new Declaration {
                    Name = "$suffix_bool",
                    Type = BuiltinTypes.Boolean,
                    DefaultValue = true,
                    Description = "suffix: a bool",
                },
                new Declaration {
                    Name = "$none_int",
                    Type = BuiltinTypes.Number,
                    DefaultValue = 42f,
                    Description = null,
                },
                new Declaration {
                    Name = "$multiline",
                    Type = BuiltinTypes.Number,
                    DefaultValue = 42f,
                    Description = "Multi-line doc comment",
                },
            };

            var actualDeclarations = new List<Declaration>(result.Declarations);

            Assert.Equal(expectedDeclarations.Count(), actualDeclarations.Count());

            for (int i = 0; i < expectedDeclarations.Count; i++)
            {
                Declaration expected = expectedDeclarations[i];
                Declaration actual = actualDeclarations[i];

                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.Type, actual.Type);
                Assert.Equal(expected.DefaultValue, actual.DefaultValue);
                Assert.Equal(expected.Description, actual.Description);
            }

        }

        [Fact]
        public void TestTypeConversion()
        {
            var source = CreateTestNode(@"
            string + string(number): {""1"" + string(1)}
            string + string(bool): {""1"" + string(true)}

            number + number(string): {1 + number(""1"")}
            number + number(bool): {1 + number(true)}

            bool and bool(string): {true and bool(""true"")}
            bool and bool(number): {true and bool(1)}
            ");

            testPlan = new TestPlanBuilder()
                .AddLine("string + string(number): 11")
                .AddLine("string + string(bool): 1True")
                .AddLine("number + number(string): 2")
                .AddLine("number + number(bool): 2")
                .AddLine("bool and bool(string): True")
                .AddLine("bool and bool(number): True")
                .GetPlan();

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            Assert.Empty(result.Diagnostics);

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;
            RunStandardTestcase();
        }

        [Theory]
        [InlineData(@"{number(""hello"")}")]
        [InlineData(@"{bool(""hello"")}")]        
        public void TestTypeConversionFailure(string test)
        {
            var source = CreateTestNode(test);
            testPlan = new TestPlanBuilder()
                .AddLine("test failure if seen")
                .GetPlan();

            Assert.Throws<FormatException>( () => {
                var compilationJob = CompilationJob.CreateFromString("input", source, dialogue.Library);
                var result = Compiler.Compile(compilationJob);

                Assert.Empty(result.Diagnostics);

                dialogue.SetProgram(result.Program);
                stringTable = result.StringTable;

                RunStandardTestcase();
            });
        }

        [Fact]
        public void TestImplicitFunctionDeclarations()
        {
            var source = CreateTestNode(@"
            {func_void_bool()}
            {func_void_bool() and bool(func_void_bool())}
            { 1 + func_void_int() }
            { ""he"" + func_void_str() }

            {func_int_bool(1)}
            {true and func_int_bool(1)}

            {func_bool_bool(false)}
            {true and func_bool_bool(false)}

            {func_str_bool(""hello"")}
            {true and func_str_bool(""hello"")}
            ");

            dialogue.Library.RegisterFunction("func_void_bool", () => true);
            dialogue.Library.RegisterFunction("func_void_int", () => 1);
            dialogue.Library.RegisterFunction("func_void_str", () => "llo");

            dialogue.Library.RegisterFunction("func_int_bool", (int i) => true);
            dialogue.Library.RegisterFunction("func_bool_bool", (bool b) => true);
            dialogue.Library.RegisterFunction("func_str_bool", (string s) => true);

            testPlan = new TestPlanBuilder()
                .AddLine("True")
                .AddLine("True")
                .AddLine("2")
                .AddLine("hello")
                .AddLine("True")
                .AddLine("True")
                .AddLine("True")
                .AddLine("True")
                .AddLine("True")
                .AddLine("True")                
                .GetPlan();

            // the library is NOT attached to this compilation job; all
            // functions will be implicitly declared
            var compilationJob = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(compilationJob);

            Assert.Empty(result.Diagnostics);

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;
            
            RunStandardTestcase();
        }

        [Theory]
        [InlineData("1", "Number")]
        [InlineData("\"hello\"", "String")]
        [InlineData("true", "Bool")]
        public void TestImplicitVariableDeclarations(string value, string typeName) {
            var source = CreateTestNode($@"
            <<set $v = {value}>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            Assert.Empty(result.Diagnostics);

            var declarations = result.Declarations.Where(d => d.Name == "$v");

            Assert.Collection(declarations, d => Assert.Equal(d.Type.Name, typeName));
        }

        [Fact]
        public void TestNestedImplicitFunctionDeclarations()
        {
            var source = CreateTestNode(@"
            {func_bool_bool(bool(func_int_bool(1)))}
            ");

            dialogue.Library.RegisterFunction("func_int_bool", (int i) => i == 1);
            dialogue.Library.RegisterFunction("func_bool_bool", (bool b) => b);

             testPlan = new TestPlanBuilder()
                .AddLine("True")
                .GetPlan();

            // the library is NOT attached to this compilation job; all
            // functions will be implicitly declared
            var compilationJob = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(compilationJob);

            Assert.Empty(result.Diagnostics);

            Assert.Equal(2, result.Declarations.Count());

            // Both declarations that resulted from the compile should be functions found on line 1
            foreach (var decl in result.Declarations) {
                Assert.Equal(3, decl.Range.Start.Line);
                Assert.IsType<FunctionType>(decl.Type);
            }

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;
            
            RunStandardTestcase();

        }

        [Fact]
        public void TestMultipleImplicitRedeclarationsOfFunctionParameterCountFail()
        {
            var source = CreateTestNode(@"
            {func(1)}
            {func(2, 2)} // wrong number of parameters (previous decl had 1)
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            Assert.Collection(result.Diagnostics, p => Assert.Contains("expects 1 parameter, but received 2", p.Message));
        }

        [Fact]
        public void TestMultipleImplicitRedeclarationsOfFunctionParameterTypeFail()
        {
            var source = CreateTestNode(@"
            {func(1)}
            {func(true)} // wrong type of parameter (previous decl had number)
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            
            Assert.Collection(result.Diagnostics, p => Assert.Contains("expects a Number, not a Bool", p.Message));   
        }

        [Fact]
        public void TestIfStatementExpressionsMustBeBoolean()
        {
            var source = CreateTestNode(@"
            <<declare $str = ""hello"" as string>>
            <<declare $bool = true>>

            <<if $bool>> // ok
            Hello
            <<endif>>

            <<if $str>> // error, must be a bool
            Hello
            <<endif>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            
            Assert.Collection(result.Diagnostics, p => Assert.Contains("Terms of 'if statement' must be Bool, not String", p.Message));
        }

        [Fact]
        public void TestBuiltinTypesAreEnumerated()
        {
            var allBuiltinTypes = BuiltinTypes.AllBuiltinTypes;

            Assert.NotEmpty(allBuiltinTypes);
        }

        [Fact]
        public void TestDeclarationBuilderCanBuildDeclarations()
        {
            // Given
            var declaration = new DeclarationBuilder()
                .WithName("$myVar")
                .WithDescription("my description")
                .WithImplicit(false)
                .WithSourceFileName("MyFile.yarn")
                .WithSourceNodeName("Test")
                .WithRange(new Yarn.Compiler.Range(0, 0, 0, 10))
                .WithType(BuiltinTypes.String)
                .Declaration;

            var expectedDeclaration = new Declaration
            {
                Name = "$myVar",
                Description = "my description",
                IsImplicit = false,
                SourceFileName = "MyFile.yarn",
                SourceNodeName = "Test",
                Range = new Yarn.Compiler.Range(0, 0, 0, 10),
                Type = BuiltinTypes.String
            };

            // Then
            Assert.Equal(expectedDeclaration, declaration);
        }

        [Fact]
        public void TestFunctionTypeBuilderCanBuildTypes() {
            // Given
            var expectedFunctionType = new FunctionType();
            expectedFunctionType.ReturnType = BuiltinTypes.String;
            expectedFunctionType.AddParameter(BuiltinTypes.String);
            expectedFunctionType.AddParameter(BuiltinTypes.Number);

            var functionType = new FunctionTypeBuilder()
                .WithParameter(BuiltinTypes.String)
                .WithParameter(BuiltinTypes.Number)
                .WithReturnType(BuiltinTypes.String)
                .FunctionType;

            // Then
            Assert.Equal(expectedFunctionType.Parameters.Count, functionType.Parameters.Count);
            Assert.Equal(expectedFunctionType.Parameters[0], functionType.Parameters[0]);
            Assert.Equal(expectedFunctionType.Parameters[1], functionType.Parameters[1]);
            Assert.Equal(expectedFunctionType.ReturnType, functionType.ReturnType);
        }
    }
}
