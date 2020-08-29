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

            IEnumerable<VariableDeclaration> declarations;

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            var expectedDeclarations = new HashSet<VariableDeclaration>() {
                new VariableDeclaration {
                    Name = "$int",
                    ReturnType = Yarn.Type.Number,
                    DefaultValue = 5f,
                },
                new VariableDeclaration {
                    Name = "$str",
                    ReturnType = Yarn.Type.String,
                    DefaultValue = "yes",
                },
                new VariableDeclaration {
                    Name = "$bool",
                    ReturnType = Yarn.Type.Bool,
                    DefaultValue = true,
                },
            };

            Assert.Equal(expectedDeclarations, result.Declarations);


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

            Compiler.Compile(compilationJob);
        }

        [Fact]
        public void TestImportingVariableDeclarations()
        {
            var source = CreateTestNode(@"
            <<set $int = 6>> // no error; declaration is imported            
            ");

            var declarations = new[] {
                new VariableDeclaration {
                    Name = "$int",
                    ReturnType = Yarn.Type.Number,
                }
            };

            CompilationJob compilationJob = CompilationJob.CreateFromString("input", source);

            // Provide the declarations
            compilationJob.VariableDeclarations = declarations;

            // Should compile with no errors because $int was declared
            var result = Compiler.Compile(compilationJob);

            // No variables are declared in the source code, so we should
            // expect an empty collection of variable declarations
            Assert.Empty(result.Declarations);
        }

        [Fact]
        void TestVariableDeclarationsDisallowDuplicates()
        {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<declare $int = 6>> // redeclaration            
            ");

            var ex = Assert.Throws<TypeException>(() =>
            {
                Compiler.Compile(CompilationJob.CreateFromString("input", source));
            });

            Assert.Contains("$int has already been declared", ex.Message);
        }

        [Fact]
        public void TestExpressionsDisallowMismatchedTypes()
        {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<set $int = ""5"">> // error, can't assign string to a variable declared int
            ");

            var ex = Assert.Throws<TypeException>(() =>
            {
                Compiler.Compile(CompilationJob.CreateFromString("input", source));
            });

            Assert.Contains("$int (Number) cannot be assigned a String", ex.Message);

        }

        [Theory]
        [InlineData(@"<<set $str = ""hi"">>")] // in commands
        [InlineData(@"{$str}")] // in inline expressions
        public void TestExpressionsDisallowUsingUndeclaredVariables(string testSource)
        {
            var source = CreateTestNode($@"
            {testSource} // error, undeclared
            ");

            var ex = Assert.Throws<TypeException>(() =>
            {
                Compiler.Compile(CompilationJob.CreateFromString("input", source));
            });

            Assert.Contains("Undeclared variable $str", ex.Message);
        }

        [Fact]
        public void TestExpressionsRequireCompatibleTypes()
        {
            var source = CreateTestNode(@"
            <<declare $int = 0>>
            <<declare $bool = false>>
            <<declare $str = """">>

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
            Compiler.Compile(CompilationJob.CreateFromString("input", source));
        }

        [Fact]
        public void TestNullNotAllowed()
        {
            var source = CreateTestNode(@"
            <<declare $err = null>> // error, null not allowed
            ");

            var ex = Assert.Throws<TypeException>(() =>
            {
                Compiler.Compile(CompilationJob.CreateFromString("input", source));
            });

            Assert.Contains("Null is not a permitted type", ex.Message);
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

            var correctSource = CreateTestNode($@"
                <<declare $bool = false>>
                {source}
            ");

            // Should compile with no exceptions
            Compiler.Compile(CompilationJob.CreateFromString("input", correctSource, dialogue.Library));

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

            if (!declared)
            {
                var ex = Assert.Throws<TypeException>(() =>
                {
                    Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));
                });

                Assert.Contains("Undeclared variable $var", ex.Message);
            }
            else
            {
                Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));
            }
        }

        [Theory]
        [InlineData("<<set $bool = func_void_bool(1)>>", "expects 0 parameters, but received 1")]
        [InlineData("<<set $bool = func_int_bool()>>", "expects 1 parameter, but received 0")]
        [InlineData("<<set $bool = func_int_bool(true)>>", "expects a Number, not a Bool")]
        [InlineData(@"<<set $bool = func_string_string_bool(""1"", 2)>>", "expects a String, not a Number")]
        [InlineData("<<set $int = func_void_bool()>>", @"\$int \(Number\) cannot be assigned a Bool")]
        [InlineData("<<set $bool = func_invalid_return()>>", "returns an invalid type")]
        [InlineData("<<set $bool = func_invalid_param(1)>>", "parameter 1's type .* cannot be used")]
        public void TestFailingFunctionSignatures(string source, string expectedExceptionMessage)
        {
            dialogue.Library.RegisterFunction("func_void_bool", () => true);
            dialogue.Library.RegisterFunction("func_int_bool", (int i) => true);
            dialogue.Library.RegisterFunction("func_int_int_bool", (int i, int j) => true);
            dialogue.Library.RegisterFunction("func_string_string_bool", (string i, string j) => true);
            dialogue.Library.RegisterFunction("func_invalid_return", () => new List<int> { 1, 2, 3 });
            dialogue.Library.RegisterFunction("func_invalid_param", (List<int> i) => true);

            var failingSource = CreateTestNode($@"
                <<declare $bool = false>>
                <<declare $int = 1>>
                {source}
            ");

            var ex = Assert.Throws<TypeException>(() =>
            {
                Compiler.Compile(CompilationJob.CreateFromString("input", failingSource, dialogue.Library));
            });

            Assert.Matches(expectedExceptionMessage, ex.Message);
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
                new VariableDeclaration {
                    Name = "$external_str",
                    ReturnType = Yarn.Type.String,
                    DefaultValue = new Value("Hello")
                },
                new VariableDeclaration {
                    Name = "$external_int",
                    ReturnType = Yarn.Type.Bool,
                    DefaultValue = new Value(true)
                },
                new VariableDeclaration {
                    Name = "$external_bool",
                    ReturnType = Yarn.Type.Number,
                    DefaultValue = new Value(42)
                },
            };

            var result = Compiler.Compile(compilationJob);

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

            Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));
        }

        [Theory]
        [InlineData(@"<<declare $str = ""hello"" as number>>")]
        [InlineData(@"<<declare $int = 1 as bool>>")]
        [InlineData(@"<<declare $bool = false as string>>")]
        public void TestExplicitTypesMustMatchValue(string test)
        {
            var source = CreateTestNode(test);

            var ex = Assert.Throws<TypeException>(() =>
            {
                var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));
            });

            Assert.Matches(@"Type \w+ does not match", ex.Message);
        }

        [Fact]
        public void TestVariableDeclarationAnnotations()
        {
            var source = CreateTestNode(@"
            <<declare $int = 42 ""a number"">>
            <<declare $str = ""Hello"" ""a string"">>
            <<declare $bool = true ""a bool"">>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            var expectedDeclarations = new HashSet<VariableDeclaration>() {
                new VariableDeclaration {
                    Name = "$int",
                    ReturnType = Yarn.Type.Number,
                    DefaultValue = 42f,
                    Description = "a number",
                },
                new VariableDeclaration {
                    Name = "$str",
                    ReturnType = Yarn.Type.String,
                    DefaultValue = "Hello",
                    Description = "a string",
                },
                new VariableDeclaration {
                    Name = "$bool",
                    ReturnType = Yarn.Type.Bool,
                    DefaultValue = true,
                    Description = "a bool",

                },
            };

            Assert.Equal(expectedDeclarations, result.Declarations);

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

                dialogue.SetProgram(result.Program);
                stringTable = result.StringTable;

                RunStandardTestcase();
            });
        }

        [Fact]
        public void TestImplicitFunctionReturnTypes()
        {
            var source = CreateTestNode(@"
            {simple_func()}
            {simple_func() and bool(simple_func())}
            { 1 + func_returning_num() }
            { ""he"" + func_returning_str() }");

            dialogue.Library.RegisterFunction("simple_func", () => true);
            dialogue.Library.RegisterFunction("func_returning_num", () => 1);
            dialogue.Library.RegisterFunction("func_returning_str", () => "llo");

            testPlan = new TestPlanBuilder()
                .AddLine("True")
                .AddLine("True")
                .AddLine("2")
                .AddLine("hello")
                .GetPlan();

            var compilationJob = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(compilationJob);

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            
            RunStandardTestcase();

        }
    }
}
