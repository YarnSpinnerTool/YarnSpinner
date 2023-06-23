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
            bool success = this.dialogue.VariableStorage.TryGetValue<int>("$smart_var", out var evaluationResult);
            
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

        [Fact]
        public void TestSmartVariablesHaveDependencies()
        {
            // Given
            var source = CreateTestNode(new[] {
                "<<declare $smart_var_1 = $smart_var_2 or $stored_var_1>>",
                "<<declare $smart_var_2 = $stored_var_2 > 5>>",
            });

            /*
            Dependency Tree:

                       $smart_var_1
                      /            \
            $stored_var_1         $smart_var_2
                                        |
                                  $stored_var_2
            */

            // When
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            // Then
            var smartVar1 = result.Declarations.Should()
                .Contain(d => d.Name == "$smart_var_1").Subject;
            var smartVar2 = result.Declarations.Should()
                .Contain(d => d.Name == "$smart_var_2").Subject;
            var storedVar1 = result.Declarations.Should()
                .Contain(d => d.Name == "$stored_var_1").Subject;
            var storedVar2 = result.Declarations.Should()
                .Contain(d => d.Name == "$stored_var_2").Subject;
            
            smartVar1.IsInlineExpansion.Should().BeTrue();
            smartVar2.IsInlineExpansion.Should().BeTrue();

            storedVar1.IsInlineExpansion.Should().BeFalse();
            storedVar2.IsInlineExpansion.Should().BeFalse();

            smartVar1.Dependents.Should().BeEmpty("nothing depends on smartVar1");
            smartVar1.Dependencies.Should().Contain(
                new[] { smartVar2, storedVar1, storedVar2 },
                "smart_var_1 depends on smart_var_2, stored_var_1, and stored_var_2"
                );

            smartVar2.Dependents.Should().Contain(smartVar1, "smart_var_1 depends on smartVar2");
            smartVar2.Dependencies.Should().Contain(new[] { storedVar2 }, "smart_var_2 depends on storedVar2");

            storedVar1.Dependents.Should().Contain(smartVar1, "smart_var_1 depends on stored_var_1");
            storedVar1.Dependencies.Should().BeEmpty("stored_var_1 doesn't depend on anything");

            storedVar2.Dependents.Should().Contain(new[] {smartVar1, smartVar2}, "smart_var_1 and smart_var_2 depend on stored_var_2");
            storedVar2.Dependencies.Should().BeEmpty("stored_var_2 doesn't depend on anything");
        }

        public class FakeVariableStorage : IVariableStorage
        {
            public Program Program { get => null; set { } }
            
            public ISmartVariableEvaluator SmartVariableEvaluator { get => null; set => _ = 0; }

            public void Clear() {}

            public VariableKind GetVariableKind(string name) => VariableKind.Unknown;

            public void SetValue(string variableName, string stringValue) {}

            public void SetValue(string variableName, float floatValue) {}

            public void SetValue(string variableName, bool boolValue) {}

            public bool TryGetValue<T>(string variableName, out T result)
            {
                result = default;
                return false;
            }
        }

        [Fact]
        public void TestVirtualMachineCanEvaluateSmartVariable()
        {
            var library = new Library();
            library.ImportLibrary(new Dialogue.StandardLibrary());
            
            var vm = new VirtualMachine(library, new FakeVariableStorage());
            var source = CreateTestNode(new[] {
                "<<declare $smart_var = 3 + 2>>",
            });
            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));
            result.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);

            vm.Program = result.Program;
            var canGetSmartVariable = vm.TryGetSmartVariable("$smart_var", out int variableValue);

            canGetSmartVariable.Should().BeTrue();
            variableValue.Should().Be(5);

        }
    }
}
