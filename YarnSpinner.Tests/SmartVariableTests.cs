using System.Linq;
using FluentAssertions;
using Xunit;
using Yarn;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{
    public class SmartVariableTests : TestBase
    {
        [Fact]
        public void TestSmartVariablesCanBeDeclared()
        {
            // Given
            var source = CreateTestNode("<<declare $smart_var = 1 + 1>>");            

            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then
            result.Diagnostics.Should().BeEmpty();

            var smartVariables = result.Declarations.Where(decl => decl.IsInlineExpansion);

            smartVariables.Should().ContainSingle(d => d.Name == "$smart_var").Which.Type.Should().Be(Types.Number);           
        }

        [InlineData("1 + 1")]
        [InlineData("number(\"2\")")]
        [InlineData("$some_other_int")]
        [Theory]
        public void TestSmartVariablesAreDynamicContent(string smartVarExpression)
        {
            // Given
            var source = CreateTestNode(new[] {
                $"<<declare $some_other_int = 2>>",
                $"<<declare $smart_var = {smartVarExpression}>>",
            });
            
            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then
            result.Diagnostics.Should().BeEmpty();

            var smartVariables = result.Declarations.Where(decl => decl.IsInlineExpansion);

            smartVariables
                .Should().ContainSingle(d => d.Name == "$smart_var")
                .Which
                .Type.Should().Be(Types.Number);  
        }

        [Fact]
        public void TestSmartVariablesCannotContainDependencyLoops()
        {
            // Given

            // Create a dependency loop: 1 -> 2 -> 3 -> 1. 
            //
            // We add the number 1 to the first variable to force the type
            // checker to interpret $smart_var_1 as a number, preventing a type
            // check failure (which is not what we're trying to test for.)
            var source = CreateTestNode(new[] {
                "<<declare $smart_var_1 = $smart_var_2 + 1>>",
                "<<declare $smart_var_2 = $smart_var_3>>",
                "<<declare $smart_var_3 = $smart_var_1>>",
            });

            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then

            // We should have an error about a dependency loop in $smart_var_1.
            // (We should also have errors about the others, but an error in one
            // is sufficient to demonstrate that a loop can be detected.)
            result.Diagnostics
                .Should().Contain(d => d.Message.Contains("Smart variable $smart_var_1 contains a dependency loop"))
                .Which.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);
        }
    }
}
