using Xunit;
using System;
using System.Collections.Generic;
using Yarn;
using System.Linq;

using Yarn.Compiler;

using FluentAssertions;
using TypeChecker;
using Xunit.Abstractions;
using System.Net.WebSockets;

namespace YarnSpinner.Tests
{
    public class TypeTests : TestBase
    {
        public TypeTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void TestVariableDeclarationsParsed()
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

            result.Diagnostics.Should().BeEmpty();

            var expectedDeclarations = new List<object>() {
                new {
                    Name = "$int",
                    Type = Types.Number,
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
                    Dependents = Enumerable.Empty<Declaration>(),
                    Dependencies = Enumerable.Empty<Declaration>(),
                },
                new {
                    Name = "$str",
                    Type = Types.String,
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
                    Dependents = Enumerable.Empty<Declaration>(),
                    Dependencies = Enumerable.Empty<Declaration>(),
                },
                new  {
                    Name = "$bool",
                    Type = Types.Boolean,
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
                    Dependents = Enumerable.Empty<Declaration>(),
                    Dependencies = Enumerable.Empty<Declaration>(),
                },
            };

            var actualDeclarations = new List<Declaration>(result.Declarations).Where(d => d.Name.StartsWith("$"));

            actualDeclarations.Should().BeEquivalentTo(expectedDeclarations, (config) =>
            {
                return config.WithTracing();
            });
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

            result.Diagnostics.Should().BeEmpty();
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
                    Type = Types.Number,
                    DefaultValue = 0,
                    SourceFileName = Declaration.ExternalDeclaration,
                }
            };

            CompilationJob compilationJob = CompilationJob.CreateFromString("input", source);

            // Provide the declarations
            compilationJob.VariableDeclarations = declarations;

            // Should compile with no errors because $int was declared
            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();

            // The only variable declarations we should know about should be
            // external
            result.Declarations.Where(d => d.Name.StartsWith("$")).Should().OnlyContain(d => d.SourceFileName == Declaration.ExternalDeclaration);
        }

        [Fact]
        public void TestVariableDeclarationsDisallowDuplicates()
        {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<declare $int = 6>> // error! redeclaration of $int        
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            result.Diagnostics.Should().ContainSingle().Which.Message.Should().Be("Redeclaration of existing variable $int");
        }

        [Fact]
        public void TestExpressionsDisallowMismatchedTypes()
        {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<set $int = ""5"">> // error, can't assign string to a variable declared int
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            result.Diagnostics.Should().ContainSingle().Which.Message.Should().Be("$int (Number) cannot be assigned a String");
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

            result.Diagnostics.Should().BeEmpty();
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

            result.Diagnostics.Should().BeEmpty();

            result.Declarations.Should().ContainSingle(d => d.Name == "$bool").Which.Type.Should().Be(Types.Boolean);
            result.Declarations.Should().ContainSingle(d => d.Name == "$str").Which.Type.Should().Be(Types.String);

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

            // We should have no diagnostics.
            result.Diagnostics.Should().BeEmpty();

            // The variable '$bool' should have an implicit declaration. The
            // type of the variable should be Boolean, because that's the return
            // type of all of the functions we declared.
            result.Declarations.Where(d => d.Name == "$bool")
                .Should().ContainSingle().Which.Type.Should().Be(Types.Boolean);
        }

        [Theory, CombinatorialData]
        public void TestNumericOperatorsAreTypeChecked([CombinatorialValues(
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
            TestOperationIsChecked(source, Types.Number);
        }

        [Theory, CombinatorialData]
        public void TestLogicOperatorsAreTypeChecked([CombinatorialValues(
            "= true and false",
            "= true or false",
            "= not true",
            "= true xor false"
            )] string operation, bool declared)
        {
            string source = CreateTestNode($@"
                {(declared ? "<<declare $var = false>>" : "")}
                <<set $var {operation}>>
            ");
            TestOperationIsChecked(source, Types.Boolean);
        }

        [Theory, CombinatorialData]
        public void TestStringOperatorsAreTypeChecked([CombinatorialValues(
            @"= ""string"" + ""otherstring"""
            )] string operation, bool declared)
        {
            string source = CreateTestNode($@"
                {(declared ? "<<declare $var = \"\">>" : "")}
                <<set $var {operation}>>
            ");
            TestOperationIsChecked(source, Types.String);
        }

        private void TestOperationIsChecked(string source, IType expectedType)
        {
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            result.Declarations.Should().Contain(d => d.Name == "$var")
                .Which.Type.Should().Be(expectedType);

            result.Diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void TestFailingFunctionDeclarationReturnType()
        {
            dialogue.Library.RegisterFunction("func_invalid_return", () => new List<int> { 1, 2, 3 });

            var source = CreateTestNode(@"Hello");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            result.Diagnostics.Select(d => d.Message).Should().ContainMatch("*not a valid return type*");
        }

        [Fact]
        public void TestFailingFunctionDeclarationParameterType()
        {
            dialogue.Library.RegisterFunction("func_invalid_param", (List<int> listOfInts) => true);

            var source = CreateTestNode(@"Hello");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            result.Diagnostics.Select(d => d.Message).Should().ContainMatch("*parameter listOfInts's type (System.Collections.Generic.List`1[System.Int32]) cannot be used in Yarn functions");
        }

        [Theory]
        [InlineData("<<set $bool = func_void_bool(1)>>", "* expects 0 parameters, not 1")]
        [InlineData("<<set $bool = func_int_bool()>>", "* expects 1 parameter, not 0")]
        [InlineData("<<set $bool = func_int_bool(true)>>", "true (Bool) is not convertible to Number")]
        [InlineData(@"<<set $bool = func_int_int_bool(""1"", 2)>>", "\"1\" (String) is not convertible to Number")]
        [InlineData(@"<<set $bool = func_string_string_bool(""1"", 2)>>", "2 (Number) is not convertible to String")]
        [InlineData("<<set $int = func_void_bool()>>", @"$int (Number) cannot be assigned a Bool")]
        [InlineData("<<set $bool = func_void_int()>>", @"$bool (Bool) cannot be assigned a Number")]
        public void TestFailingFunctionSignatures(string source, string expectedExceptionMessage)
        {
            dialogue.Library.RegisterFunction("func_void_int", () => 1);
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

            var diagnosticMessages = result.Diagnostics.Select(d => d.Message);

            diagnosticMessages.Should().ContainMatch(expectedExceptionMessage);
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
                    Type = Types.String,
                    DefaultValue = "Hello",
                },
                new Declaration {
                    Name = "$external_int",
                    Type = Types.Boolean,
                    DefaultValue = true,
                },
                new Declaration {
                    Name = "$external_bool",
                    Type = Types.Number,
                    DefaultValue = 42,
                },
            };

            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();

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

            result.Diagnostics.Should().BeEmpty();

            var variableDeclarations = result.Declarations.Where(d => d.Name.StartsWith("$"));

            variableDeclarations.Should().Contain(d => d.Name == "$str").Which.Type.Should().Be(Types.String);
            variableDeclarations.Should().Contain(d => d.Name == "$int").Which.Type.Should().Be(Types.Number);
            variableDeclarations.Should().Contain(d => d.Name == "$bool").Which.Type.Should().Be(Types.Boolean);
        }

        [Theory]
        [InlineData(@"<<declare $str = ""hello"" as number>>")]
        [InlineData(@"<<declare $int = 1 as bool>>")]
        [InlineData(@"<<declare $bool = false as string>>")]
        public void TestExplicitTypesMustMatchValue(string test)
        {
            var source = CreateTestNode(test);

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            result.Diagnostics
                .Should().Contain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error)
                .Which.Message.Should().MatchRegex(@"\$(.+?)'s type \(.+?\) must be .+?");
        }

        [Fact]
        public void TestJumpExpressionsMustBeStrings()
        {
            var source = CreateTestNode(@"<<set $x = 5>>
            <<jump {$x}>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            result.Diagnostics
                .Should().Contain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error)
                .Which.Message.Should().Contain("jump statement's expression must be a String, not a Number");
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

            result.Diagnostics.Should().BeEmpty();

            var expectedDeclarations = new List<Declaration>() {
                new Declaration {
                    Name = "$prefix_int",
                    Type = Types.Number,
                    DefaultValue = 42f,
                    Description = "prefix: a number",
                },
                new Declaration {
                    Name = "$prefix_str",
                    Type = Types.String,
                    DefaultValue = "Hello",
                    Description = "prefix: a string",
                },
                new Declaration {
                    Name = "$prefix_bool",
                    Type = Types.Boolean,
                    DefaultValue = true,
                    Description = "prefix: a bool",
                },
                new Declaration {
                    Name = "$suffix_int",
                    Type = Types.Number,
                    DefaultValue = 42f,
                    Description = "suffix: a number",
                },
                new Declaration {
                    Name = "$suffix_str",
                    Type = Types.String,
                    DefaultValue = "Hello",
                    Description = "suffix: a string",
                },
                new Declaration {
                    Name = "$suffix_bool",
                    Type = Types.Boolean,
                    DefaultValue = true,
                    Description = "suffix: a bool",
                },
                new Declaration {
                    Name = "$none_int",
                    Type = Types.Number,
                    DefaultValue = 42f,
                    Description = null,
                },
                new Declaration {
                    Name = "$multiline",
                    Type = Types.Number,
                    DefaultValue = 42f,
                    Description = "Multi-line doc comment",
                },
            };

            var actualDeclarations = new List<Declaration>(result.Declarations).Where(d => d.Name.StartsWith("$"));

            actualDeclarations.Should().BeEquivalentTo(expectedDeclarations, config =>
                config
                    .Including(o => o.Name)
                    .Including(o => o.Type)
                    .Including(o => o.DefaultValue)
                    .Including(o => o.Description)
            );

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

            result.Diagnostics.Should().BeEmpty();

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

            Action act = () =>
            {

                var compilationJob = CompilationJob.CreateFromString("input", source, dialogue.Library);
                var result = Compiler.Compile(compilationJob);

                result.Diagnostics.Should().BeEmpty();

                dialogue.SetProgram(result.Program);
                stringTable = result.StringTable;

                RunStandardTestcase();
            };

            act.Should().ThrowExactly<FormatException>();
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

            // A list of function names that are referred to in the above source
            // code, and the method signatures that should be inferred for them
            var expectedFunctions = new (string Name, string Description)[]{
                ("func_void_bool", "() -> Bool"),
                ("func_void_int", "() -> Number"),
                ("func_void_str", "() -> String"),
                ("func_int_bool", "(Number) -> Bool"),
                ("func_bool_bool", "(Bool) -> Bool"),
                ("func_str_bool", "(String) -> Bool"),
            };

            // all functions will be implicitly declared
            var compilationJob = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(compilationJob);

            var functions = result.Declarations.Where(d => d.Type is FunctionType);


            foreach (var expectedFunction in expectedFunctions)
            {
                functions.Should().ContainEquivalentOf(new
                {
                    Name = expectedFunction.Name,
                    Type = new
                    {
                        Description = expectedFunction.Description
                    }
                });
            }

            result.Diagnostics.Should().BeEmpty();

            dialogue.Library.RegisterFunction("func_void_bool", () => true);
            dialogue.Library.RegisterFunction("func_void_int", () => 1);
            dialogue.Library.RegisterFunction("func_void_str", () => "llo");

            dialogue.Library.RegisterFunction("func_int_bool", (int i) => true);
            dialogue.Library.RegisterFunction("func_bool_bool", (bool b) => true);
            dialogue.Library.RegisterFunction("func_str_bool", (string s) => true);

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            RunStandardTestcase();
        }

        [Theory]
        [InlineData("1", "Number")]
        [InlineData("\"hello\"", "String")]
        [InlineData("true", "Bool")]
        public void TestImplicitVariableDeclarations(string value, string typeName)
        {
            var source = CreateTestNode($@"
            <<set $v = {value}>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().BeEmpty();

            result.Declarations.Should().ContainSingle(d => d.Name == "$v")
                .Which.Type.Name.Should().Be(typeName);
        }

        [Fact]
        public void TestNestedImplicitFunctionDeclarations()
        {
            var source = CreateTestNode(@"
            {func_bool_bool(func_int_bool(1) and true) and true}
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

            result.Diagnostics.Should().BeEmpty();

            var expectedIntBoolFunctionType = new FunctionTypeBuilder().WithParameter(Types.Number).WithReturnType(Types.Boolean).FunctionType;
            var expectedBoolBoolFunctionType = new FunctionTypeBuilder().WithParameter(Types.Boolean).WithReturnType(Types.Boolean).FunctionType;

            result.Declarations.Should().ContainSingle(d => d.Name == "func_int_bool")
                .Which.Type.Should().BeEquivalentTo(expectedIntBoolFunctionType);

            result.Declarations.Should().ContainSingle(d => d.Name == "func_bool_bool")
                .Which.Type.Should().BeEquivalentTo(expectedBoolBoolFunctionType);


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

            result.Diagnostics.Select(d => d.Message).Should().ContainMatch("func was called elsewhere with 1 parameter, but is called with 2 parameters here");
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

            result.Diagnostics.Should().ContainSingle().Which.Message.Contains("if statement's expression must be a Boolean, not a String");
        }

        [Fact]
        public void TestTypesAreEnumerated()
        {
            var allTypes = Types.AllBuiltinTypes;

            allTypes.Should().NotBeEmpty();
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
                .WithType(Types.String)
                .Declaration;

            var expectedDeclaration = new Declaration
            {
                Name = "$myVar",
                Description = "my description",
                IsImplicit = false,
                SourceFileName = "MyFile.yarn",
                SourceNodeName = "Test",
                Range = new Yarn.Compiler.Range(0, 0, 0, 10),
                Type = Types.String
            };

            // Then
            declaration.Should().BeEquivalentTo(expectedDeclaration);
        }

        [Fact]
        public void TestFunctionTypeBuilderCanBuildTypes()
        {
            // Given
            var expectedFunctionType = new FunctionType(Types.String, Types.String, Types.Number);

            var functionType = new FunctionTypeBuilder()
                .WithParameter(Types.String)
                .WithParameter(Types.Number)
                .WithReturnType(Types.String)
                .FunctionType;

            // Then
            expectedFunctionType.Parameters.Count.Should().Be(functionType.Parameters.Count);
            expectedFunctionType.Parameters[0].Should().Be(functionType.Parameters[0]);
            expectedFunctionType.Parameters[1].Should().Be(functionType.Parameters[1]);
            expectedFunctionType.ReturnType.Should().Be(functionType.ReturnType);
        }

        [Fact]
        public void TestSolverCanResolveConvertabilityConstraints()
        {
            var boolType = Types.Boolean;
            var anyType = Types.Any;
            var unknownType1 = new TypeChecker.TypeVariable("T1", null);
            var unknownType2 = new TypeChecker.TypeVariable("T2", null);

            // Attempt to solve the following system of equations:
            // T1 c> Any ; T1 == T2 ; T2 == Boolean
            var constraints = new TypeChecker.TypeConstraint[] {
                new TypeChecker.TypeConvertibleConstraint(unknownType1, anyType),
                new TypeChecker.TypeEqualityConstraint(unknownType1, unknownType2),
                new TypeChecker.TypeEqualityConstraint(unknownType2, boolType),
            };

            var diagnostics = new List<Diagnostic>();

            Substitution solution = null;

            bool hasSolution = TypeChecker.Solver.TrySolve(constraints, Types.AllBuiltinTypes.OfType<TypeBase>(), diagnostics, ref solution);

            using (new FluentAssertions.Execution.AssertionScope())
            {
                diagnostics.Should().BeEmpty();
                hasSolution.Should().BeTrue();

                // T1 should resolve to Bool
                solution.TryResolveTypeVariable(unknownType1, out var result1).Should().BeTrue();
                result1.Should().Be(boolType);

                // T2 should also resolve to Bool
                solution.TryResolveTypeVariable(unknownType2, out var result2).Should().BeTrue();
                result2.Should().Be(boolType);
            }
        }

        [Fact]
        public void TestSolverCannotResolveMismatchedConvertabilityConstraint()
        {
            var boolType = Types.Boolean;
            var numberType = Types.Number;
            var unknownType1 = new TypeChecker.TypeVariable("T1", null);
            var unknownType2 = new TypeChecker.TypeVariable("T2", null);

            // Attempt to solve the following unresolvable system of equations:
            // T1 c> T2 ; T1 == Bool ; T2 == Number
            var constraints = new TypeChecker.TypeConstraint[] {
                new TypeChecker.TypeConvertibleConstraint(unknownType1, unknownType2),
                new TypeChecker.TypeEqualityConstraint(unknownType1, boolType),
                new TypeChecker.TypeEqualityConstraint(unknownType2, numberType),
            };

            var diagnostics = new List<Diagnostic>();

            Substitution solution = null;

            var hasSolution = TypeChecker.Solver.TrySolve(constraints, Types.AllBuiltinTypes.OfType<TypeBase>(), diagnostics, ref solution);

            using (new FluentAssertions.Execution.AssertionScope())
            {
                hasSolution.Should().BeFalse();
            }

        }

        /// <summary>
        /// A type constraint that's used for testing the internal logic of the
        /// type solver.
        /// </summary>
        class AbstractTypeConstraint : TypeConstraint
        {
            public string Name { get; }
            public AbstractTypeConstraint(string name) => this.Name = name;
            public override string ToString() => this.Name;
            // Overridden abstract methods
            public override IEnumerable<TypeVariable> AllVariables => Array.Empty<TypeVariable>();
            public override IEnumerable<TypeConstraint> DescendantsAndSelf => new[] { this };
            public override IEnumerable<TypeConstraint> Children => Array.Empty<TypeConstraint>();
            public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes) => this;
        }

        [Fact]
        public void TestConstraintsCanConvertToDisjunctiveNormalForm()
        {
            var a = new AbstractTypeConstraint("A");
            var b = new AbstractTypeConstraint("B");
            var c = new AbstractTypeConstraint("C");
            var d = new AbstractTypeConstraint("D");
            var e = new AbstractTypeConstraint("E");
            var f = new AbstractTypeConstraint("F");

            // Input: and(or(a,b), or(c,and(d,e)), f)
            var testConstraint = new ConjunctionConstraint(new TypeConstraint[] {
                new DisjunctionConstraint(new[] {
                    a,b
                }),
                new DisjunctionConstraint(new TypeConstraint[] {
                    c, new ConjunctionConstraint(d,e),
                }),
                f
            });

            // Expected result:
            // or(and(a,c,f), and(b,c,f), and(a,d,e,f), and(b,d,e,f))
            DisjunctionConstraint resultConstraint = Solver.ToDisjunctiveNormalForm(testConstraint);

            resultConstraint.Children.Should().AllBeOfType<ConjunctionConstraint>();
            resultConstraint.Children.Should().HaveCount(4);
            resultConstraint.Children.ElementAt(0).Children.Should().BeEquivalentTo(new[] { a, c, f });
            resultConstraint.Children.ElementAt(1).Children.Should().BeEquivalentTo(new[] { a, d, e, f });
            resultConstraint.Children.ElementAt(2).Children.Should().BeEquivalentTo(new[] { b, c, f });
            resultConstraint.Children.ElementAt(3).Children.Should().BeEquivalentTo(new[] { b, d, e, f });
        }

        [Fact]
        public void TestUserDefinedTypesAreProvided()
        {
            var source = CreateTestNode(@"
<<enum MyEnum>>
<<case One>>
<<case Two>>
<<case Three>>
<<endenum>>
");

            var compilationJob = CompilationJob.CreateFromString("input", source);
            compilationJob.AllowPreviewFeatures = true;
            var result = Compiler.Compile(compilationJob);

            result.ContainsErrors.Should().BeFalse();
            result.UserDefinedTypes.Should().NotBeEmpty();

            var userEnum = result.UserDefinedTypes
                .Should().Contain(i => i.Name == "MyEnum")
                .Which.Should().BeOfType<EnumType>();

            result.UserDefinedTypes.Should().NotContain(Types.String, "String is built-in, and not user-defined");
        }
    }
}
