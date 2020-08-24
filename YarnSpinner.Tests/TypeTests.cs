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
            <<declare $bool = true>>

            // These value changes are allowed, 
            // because they match the declared type
            <<set $int = 6>>
            <<set $str = ""no"">>
            <<set $bool = false>>
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
        void TestVariableDeclarationsDisallowDuplicates() {
            var source = CreateTestNode(@"
            <<declare $int = 5>>
            <<declare $int = 6>> // redeclaration            
            ");

            var ex = Assert.Throws<TypeException>(() => {
                Compiler.Compile(CompilationJob.CreateFromString("input", source));
            });
            
            throw new NotImplementedException();
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
    }
}
