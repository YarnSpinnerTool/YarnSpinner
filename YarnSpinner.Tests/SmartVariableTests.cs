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

        [Fact]
        public void TestSmartVariablesCanTakeAnyValidExpressionType()
        {
            // Given
            var source = CreateTestNode(new[] {
                "<<declare $smart_var_number = 1 + 1>>",
                "<<declare $smart_var_string = \"hello\" + \" yes\">>",
                "<<declare $smart_var_bool = true || false>>",
            });
        
            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
        
            // Then
            result.Diagnostics.Should().BeEmpty();

            var smartVariables = result.Declarations.Where(decl => decl.IsInlineExpansion);
            smartVariables.Should().HaveCount(3);
            
            smartVariables.Should().Contain(v => v.Name == "$smart_var_number").Which.Type.Should().Be(Types.Number);
            smartVariables.Should().Contain(v => v.Name == "$smart_var_string").Which.Type.Should().Be(Types.String);
            smartVariables.Should().Contain(v => v.Name == "$smart_var_bool").Which.Type.Should().Be(Types.Boolean);
        }

        [Fact]
        public void TestSmartVariablesCanReferenceOtherSmartVariables()
        {
            // Given
            var source = CreateTestNode(new[] {
                "<<declare $smart_var_number = 1 + 1>>",
                "<<declare $smart_var_bool = $smart_var_number == 2>>",
                "{$smart_var_bool}"
            });

            var testPlan = new TestPlanBuilder()
                .AddLine("True")
                .AddStop()
                .GetPlan();

            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then
            RunTestPlan(result, testPlan);
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
        public void TestSmartVariablesCanBeEvaluatedInScript()
        {
            // Given
            this.testPlan = new TestPlanBuilder()
                .AddLine("2")
                .AddLine("pass")
                .AddStop()
                .GetPlan();

            var source = CreateTestNode(new[] {
                "<<declare $smart_var = 1 + 1>>",
                // smart variables can be used in inline expressions...
                "{$smart_var}", 
                // ...and can be used in any expression
                "<<if string($smart_var) == \"2\">>", 
                "pass",
                "<<endif>>",
            });

            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then
            RunTestPlan(result, testPlan);
        }

        [Fact]
        public void TestSmartVariablesCompileToNodes()
        {
            // Given
            var source = CreateTestNode(new[] {
                "<<declare $smart_var_number = 1 + 1>>",
                "<<declare $smart_var_string = \"hello\" + \" yes\">>",
                "<<declare $smart_var_bool = true || false>>",
            });
        
            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then
            result.Program.SmartVariableNodes.Should().HaveCount(3);
            result.Program.SmartVariableNodes.Select(n => n.Name).Should().Contain(new[] {
                "$smart_var_number",
                "$smart_var_string",
                "$smart_var_bool",
            });
        }

        [Fact]
        public void TestSmartVariablesCanBeEvaluatedExternally()
        {
            // Given
            var source = CreateTestNode("<<declare $smart_var = 1 + 1>>");
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            this.dialogue.SetProgram(result.Program);

            // When
            bool success = this.dialogue.TryGetVariable<int>("$smart_var", out var evaluationResult);
            
            // Then
            success.Should().BeTrue();
            evaluationResult.Should().Be(2);
        }

        [Fact]
        public void TestSmartVariablesCannotContainDependencyLoops()
        {
            // Given

            // Create a dependency loop: 1 -> 2 -> 3 -> 1. 
            //
            // We add the number 1 to the first variable to force the type
            // checker to interpret all of the variables as numbers, preventing
            // a type check failure (which is not what we're trying to test
            // for.)
            var source = CreateTestNode(new[] {
                "<<declare $smart_var_1 = $smart_var_2 + 1>>",
                "<<declare $smart_var_2 = $smart_var_3>>",
                "<<declare $smart_var_3 = $smart_var_1>>", // error! loop created!
            });

            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then

            // We should have an error about a dependency loop in $smart_var_1.
            // (We should also have errors about the others, but an error in one
            // is sufficient to demonstrate that a loop can be detected.)
            result.Diagnostics
                .Should().Contain(d => d.Message.Contains("Smart variables cannot contain reference loops (referencing $smart_var_1 here creates a loop for the smart variable $smart_var_1)"))
                .Which.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);
        }

        [Fact]
        public void TestSmartVariablesCanBeChained()
        {
            // Given
            var source = CreateTestNode(new[] {
                "<<declare $smart_var_1 = $smart_var_2>>",
                "<<declare $smart_var_2 = $smart_var_3>>",
                "<<declare $smart_var_3 = 1 + 1>>",
                "{$smart_var_1}"
            });

            this.testPlan = new TestPlanBuilder()
                .AddLine("2")
                .AddStop()
                .GetPlan();

            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
        
            // Then
            RunTestPlan(result, testPlan);
        }

        [Fact]
        public void TestSmartVariablesCannotBeWrittenTo()
        {
            // Given
            var source = CreateTestNode(new[] {
                "<<declare $smart_var = 1 + 1>>",
                "<<set $smart_var to 3>>", // error!
            });
        
            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then
            result.Diagnostics.Should()
                .Contain(d => d.Message == "$smart_var cannot be modified (it's a smart variable and is always equal to 1 + 1)")
                .Which.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);
        }
    }
}
