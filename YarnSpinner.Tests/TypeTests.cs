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
		public TypeTests() : base() {
        }

        [Fact]
        void TestVariableDeclarationsParsed() {
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
                    name = "$int",
                    type = Value.Type.Number,
                    defaultValue = new Value(5)
                },
                new VariableDeclaration {
                    name = "$str",
                    type = Value.Type.String,
                    defaultValue = new Value("yes")
                },
                new VariableDeclaration {
                    name = "$bool",
                    type = Value.Type.Bool,
                    defaultValue = new Value(true)
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

            var compilationJob = new CompilationJob {
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
                    name = "$int",
                    type = Value.Type.Number,
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
        void TestVariableDeclarationsDisallowDuplicates() {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<declare $int = 6>> // redeclaration            
            ");

            var ex = Assert.Throws<TypeException>(() => {
                Compiler.Compile(CompilationJob.CreateFromString("input", source));
            });

            Assert.Contains("$int has already been declared", ex.Message);
        }

        [Fact]
        public void TestExpressionsDisallowMismatchedTypes() {
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

        [Fact]
        public void TestExpressionsDisallowUsingUndeclaredVariables() {
            var source = CreateTestNode(@"
            <<set $str = ""hello"">> // error, undeclared
            ");

            var ex = Assert.Throws<TypeException>(() =>
            {
                Compiler.Compile(CompilationJob.CreateFromString("input", source));
            });

            Assert.Contains("Undeclared variable $str", ex.Message);
        }

        [Fact]
        public void TestExpressionsRequireCompatibleTypes() {
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

            var ex = Assert.Throws<TypeException>( () => {
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
            dialogue.library.RegisterFunction("func_void_bool", () => true);
            dialogue.library.RegisterFunction("func_int_bool", (int i) => true);
            dialogue.library.RegisterFunction("func_int_int_bool", (int i, int j) => true);
            dialogue.library.RegisterFunction("func_string_string_bool", (string i, string j) => true);

            var correctSource = CreateTestNode($@"
                <<declare $bool = false>>
                {source}
            ");

            // Should compile with no exceptions
            Compiler.Compile(CompilationJob.CreateFromString("input", correctSource, dialogue.library));

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
            )] string operation, bool declared ) {

            string source = CreateTestNode($@"
                {(declared ? "<<declare $var = 0>>" : "")}
                <<set $var {operation}>>
            ");

            if (!declared) {
                var ex = Assert.Throws<TypeException>(() => {
                    Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.library));
                });

                Assert.Contains("Undeclared variable $var", ex.Message);
            } else {
                Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.library));
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
            dialogue.library.RegisterFunction("func_void_bool", () => true);
            dialogue.library.RegisterFunction("func_int_bool", (int i) => true);
            dialogue.library.RegisterFunction("func_int_int_bool", (int i, int j) => true);
            dialogue.library.RegisterFunction("func_string_string_bool", (string i, string j) => true);
            dialogue.library.RegisterFunction("func_invalid_return", () => new List<int>{1,2,3});
            dialogue.library.RegisterFunction("func_invalid_param", (List<int> i) => true);

            var failingSource = CreateTestNode($@"
                <<declare $bool = false>>
                <<declare $int = 1>>
                {source}
            ");

            var ex = Assert.Throws<TypeException>(() => {
                Compiler.Compile(CompilationJob.CreateFromString("input", failingSource, dialogue.library));
            });

            Assert.Matches(expectedExceptionMessage, ex.Message);        
        }
    }
}
