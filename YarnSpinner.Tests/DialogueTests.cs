﻿using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Yarn;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{


    public class DialogueTests : TestBase
    {
        public DialogueTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

        [Fact]
        public void TestNodeExists()
        {
            var path = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");

            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;

            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram(result.Program);

            dialogue.NodeExists("Sally").Should().BeTrue();

            // Test clearing everything
            dialogue.UnloadAll();

            dialogue.NodeExists("Sally").Should().BeFalse();

        }

        [Fact]
        public void TestAnalysis()
        {

            ICollection<Yarn.Analysis.Diagnosis> diagnoses;
            Yarn.Analysis.Context context;

            // this script has the following variables:
            // $foo is read from and written to
            // $bar is written to but never read
            // this means that there should be one diagnosis result
            context = new Yarn.Analysis.Context(typeof(Yarn.Analysis.UnusedVariableChecker));

            var path = Path.Combine(TestDataPath, "AnalysisTest.yarn");

            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;

            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.Analyse(context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis());

            diagnoses.Count.Should().Be(1);
            diagnoses.First().message.Should().Contain("Variable $bar is assigned, but never read from");

            dialogue.UnloadAll();

            context = new Yarn.Analysis.Context(typeof(Yarn.Analysis.UnusedVariableChecker));

            result = Compiler.Compile(CompilationJob.CreateFromFiles(new[] {
                Path.Combine(SpaceDemoScriptsPath, "Ship.yarn"),
                Path.Combine(SpaceDemoScriptsPath, "Sally.yarn"),
            }, dialogue.Library));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram(result.Program);

            dialogue.Analyse(context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis());

            // This script should contain no unused variables
            diagnoses.Should().BeEmpty();
        }

        [Fact]
        public void TestDumpingCode()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn");
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            var byteCode = result.DumpProgram();
            byteCode.Should().NotBeNull();

        }

        [Fact]
        public void TestMissingNode()
        {
            var path = Path.Combine(TestDataPath, "TestCases", "Smileys.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram(result.Program);

            runtimeErrorsCauseFailures = false;

            var settingInvalidNode = new Action(() => dialogue.SetNode("THIS NODE DOES NOT EXIST"));
            settingInvalidNode.Should().Throw<DialogueException>();
        }

        [Fact]
        public void TestGettingCurrentNodeName()
        {

            string path = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");

            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;

            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram(result.Program);

            // dialogue should not be running yet
            dialogue.CurrentNode.Should().BeNull();

            dialogue.SetNode("Sally");
            dialogue.CurrentNode.Should().Be("Sally");

            dialogue.Stop();
            // Current node should now be null
            dialogue.CurrentNode.Should().BeNull();
        }

        [Fact]
        public void TestGettingTags()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram(result.Program);

            var source = dialogue.GetHeaderValue("LearnMore", "tags").Split(' ');

            source.Should().NotBeNull();

            source.Should().NotBeEmpty();

            source.First().Should().Be("rawText");
        }

        [Fact]
        public void TestPrepareForLine()
        {
            var path = Path.Combine(TestDataPath, "TaggedLines.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            stringTable = result.StringTable;

            bool prepareForLinesWasCalled = false;

            dialogue.PrepareForLinesHandler = (lines) =>
            {
                // When the Dialogue realises it's about to run the Start
                // node, it will tell us that it's about to run these two
                // line IDs
                lines.Should().HaveCount(2);
                lines.Should().Contain("line:test1");
                lines.Should().Contain("line:test2");

                // Ensure that these asserts were actually called
                prepareForLinesWasCalled = true;
            };

            dialogue.SetProgram(result.Program);
            dialogue.SetNode("Start");

            prepareForLinesWasCalled.Should().BeTrue();
        }


        [Fact]
        public void TestFunctionArgumentTypeInference()
        {

            // Register some functions
            dialogue.Library.RegisterFunction("ConcatString", (string a, string b) => a + b);
            dialogue.Library.RegisterFunction("AddInt", (int a, int b) => a + b);
            dialogue.Library.RegisterFunction("AddFloat", (float a, float b) => a + b);
            dialogue.Library.RegisterFunction("NegateBool", (bool a) => !a);

            // Run some code to exercise these functions
            var source = CreateTestNode(@"
            <<declare $str = """">>
            <<declare $int = 0>>
            <<declare $float = 0.0>>
            <<declare $bool = false>>

            <<set $str = ConcatString(""a"", ""b"")>>
            <<set $int = AddInt(1,2)>>
            <<set $float = AddFloat(1,2)>>
            <<set $bool = NegateBool(true)>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            result.Diagnostics.Should().BeEmpty();

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.SetNode("Start");

            dialogue.LineHandler = (line) => { };
            dialogue.OptionsHandler = (opts) => { dialogue.SetSelectedOption(opts.Options.First().ID); };
            dialogue.CommandHandler = (command) => { };

            do
            {
                dialogue.Continue();
            } while (dialogue.IsActive);

            // The values should be of the right type and value

            this.storage.TryGetValue<string>("$str", out var strValue);
            strValue.Should().Be("ab");

            this.storage.TryGetValue<float>("$int", out var intValue);
            intValue.Should().Be(3);

            this.storage.TryGetValue<float>("$float", out var floatValue);
            floatValue.Should().Be(3);

            this.storage.TryGetValue<bool>("$bool", out var boolValue);
            boolValue.Should().BeFalse();
        }

        [Fact]
        public void TestDialogueStorageCanRetrieveValues()
        {
            // Given
            var source = CreateTestNode(new[] {
                "<<declare $numVar = 42>>",
                "<<declare $stringVar = \"hello\">>",
                "<<declare $boolVar = true>>",
            });
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);
            dialogue.SetProgram(result.Program);

            // When
            var canGetNumber = dialogue.VariableStorage.TryGetValue<int>("$numVar", out var numResult);
            var canGetString = dialogue.VariableStorage.TryGetValue<string>("$stringVar", out var stringResult);
            var canGetBool = dialogue.VariableStorage.TryGetValue<bool>("$boolVar", out var boolResult);

            // Then
            canGetNumber.Should().BeTrue();
            canGetString.Should().BeTrue();
            canGetBool.Should().BeTrue();

            numResult.Should().Be(42);
            stringResult.Should().Be("hello");
            boolResult.Should().Be(true);
        }

    }
}

