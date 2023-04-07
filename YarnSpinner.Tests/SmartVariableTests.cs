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

    }
}
