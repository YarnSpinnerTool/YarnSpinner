using Xunit;
using System.Linq;
using FluentAssertions;
using System.IO;
using YarnLanguageServer.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Yarn;
using System.Text.RegularExpressions;

namespace YarnLanguageServer.Tests
{
    public class ActionDeclarationTests
    {
        private static string WorkspacePath = Path.Combine(TestUtility.PathToTestData, "ActionDeclarationTests");
        private static string ProjectPath = Path.Combine(WorkspacePath, "Test.yarnproject");

        [Fact]
        public void CSharpData_DocumentationCommentsAreExtracted() {
            var path = Path.Combine(TestUtility.PathToTestData, "TestWorkspace", "Project1", "ExampleCommands.cs");

            File.Exists(path).Should().BeTrue($"{path} should exist on disk");

            var uri = DocumentUri.FromFileSystemPath(path).ToUri();

            var source = File.ReadAllText(path);

            var data = CSharpFileData.ParseActionsFromCode(source, uri);

            var action = data.Should().ContainSingle(a => a.YarnName == "command_with_complex_documentation").Subject;

            action.Documentation.Should().Be("This command has <c>nested XML</c> nodes.");
        }
        
        [Fact]
        public void ActionDeclaration_AllYarnFunctions_AreCalled() {
            var workspace = new Workspace();
            workspace.Root = WorkspacePath;
            workspace.Initialize();

            var diagnostics = workspace.GetDiagnostics().SelectMany(d => d.Value);

            diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Warning);
            diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

            var compiledOutput = workspace.Projects.Single().CompileProject(false, Yarn.Compiler.CompilationJob.Type.FullCompilation);
            var compiledProgram = compiledOutput.Program;
            var declsNode = compiledProgram.Nodes.Single(n => n.Key == "ActionDeclarations").Value;

            var functions = workspace.Projects.Single().Functions;
            
            foreach (var func in functions) {
                declsNode.Instructions.Should().Contain(i => i.Opcode == Yarn.Instruction.Types.OpCode.CallFunc && i.Operands[0].StringValue == func.YarnName, $"the node should call '{func.YarnName}()'");
            }
        }

        [Fact]
        public void ActionDeclarations_AreAllPresentInLibrary() {
            var workspace = new Workspace();
            workspace.Initialize();

            var functions = workspace.Projects.Single().Functions;

            var standardLibrary = new Dialogue.StandardLibrary();

            var functionsToOmit = new[] {
                "visited",
                "visited_count"
            };

            foreach (var decl in functions) {
                if (functionsToOmit.Contains(decl.YarnName)) {
                    continue;
                }

                var functionImpl = standardLibrary.GetFunction(decl.YarnName);

                CheckImplementationMatchesDeclaration(functionImpl, decl);
            }
        }

        [Fact]
        public void LibraryMethods_AreAllDeclared() {
            var workspace = new Workspace();
            workspace.Initialize();

            var functions = workspace.Projects.Single().Functions;

            var standardLibrary = new Dialogue.StandardLibrary();

            var patternsToOmit = new[] {
                new Regex(@"^Number\."),
                new Regex(@"^String\."),
                new Regex(@"^Bool\."),
            };

            foreach (var registeredFunction in standardLibrary.Delegates)
            {
                var name = registeredFunction.Key;
                var impl = registeredFunction.Value;

                if (patternsToOmit.Any(pattern => pattern.IsMatch(name)))
                {
                    continue;
                }

                var decl = functions.Should().ContainSingle(f => f.YarnName == name).Subject;

                CheckImplementationMatchesDeclaration(impl, decl);
            }
        }

        private static void CheckImplementationMatchesDeclaration(System.Delegate impl, Action decl)
        {
            var methodReturnType = impl.Method.ReturnType;
            var declReturnType = decl.ReturnType;

            BuiltinTypes.TypeMappings[methodReturnType].Should().Be(declReturnType, $"{decl.YarnName}'s return type is declared as {declReturnType}");

            decl.Parameters.Should().HaveCount(impl.Method.GetParameters().Length);
            var declParameters = decl.Parameters;
            var implParameters = impl.Method.GetParameters();

            for (int i = 0; i < declParameters.Count(); i++)
            {
                var declParameter = declParameters.ElementAt(i);
                var implParameter = implParameters.ElementAt(i);
                BuiltinTypes.TypeMappings[implParameter.ParameterType].Should().Be(declParameter.Type);
            }
        }
    }
}
