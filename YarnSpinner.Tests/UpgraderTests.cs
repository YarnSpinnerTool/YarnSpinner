using Xunit;
using System;
using System.IO;
using Yarn.Compiler.Upgrader;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{

    public class UpgraderTests : TestBase
    {

        // Test every file in Tests/TestCases
        [Theory]
        [MemberData(nameof(FileSources), "Upgrader/V1toV2")]
        public void TestUpgradingV1toV2(string file)
        {

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"INFO: Loading file {file}");

            storage.Clear();

            var originalFilePath = Path.Combine(TestDataPath, file);
            var upgradedFilePath = Path.ChangeExtension(originalFilePath, ".upgraded.yarn");
            var testPlanFilePath = Path.ChangeExtension(originalFilePath, ".testplan");

            LoadTestPlan(testPlanFilePath);

            var originalContents = File.ReadAllText(originalFilePath);

            var fileName = Path.GetFileNameWithoutExtension(originalFilePath);

            var upgradedContents = LanguageUpgrader.UpgradeScript(originalContents, fileName, UpgradeType.Version1to2, out var replacements);

            var expectedContents = File.ReadAllText(upgradedFilePath);

            // Verify that the upgrade did what we expect
            Assert.Equal(expectedContents, upgradedContents);

            // Compile this upgraded source
            Compiler.CompileString(upgradedContents, fileName, out var program, out stringTable);

            // Execute the program and verify thats output matches the test
            // plan
            dialogue.SetProgram(program);

            // If this file contains a Start node, run the test case
            // (otherwise, we're just testing its parsability, which we did
            // in the last line)
            if (dialogue.NodeExists("Start"))
            {
                RunStandardTestcase();
            }
        }

        [Fact]
        public void TestTextReplacement()
        {
            var text = "Keep delete keep\nreplace keep";
            var expectedReplacement = "Keep keep\nnew keep";

            var replacements = new[] {
                new TextReplacement() {
                    Start = 5,
                    OriginalText = "delete ",
                    ReplacementText = ""
                },
                new TextReplacement() {
                    Start = 17,
                    OriginalText = "replace",
                    ReplacementText = "new"
                },                
            };

            var replacedText = LanguageUpgrader.ApplyReplacements(text, replacements);

            Assert.Equal(expectedReplacement, replacedText);
        }

        [Fact]
        public void TestInvalidReplacementThrows()
        {
            var text = "Keep keep";
            
            var replacements = new[] {
                new TextReplacement() {
                    Start = 5,
                    OriginalText = "delete ", // the replacement expects to see  "delete " here, but it will see "keep" instead
                    ReplacementText = ""
                },                
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => LanguageUpgrader.ApplyReplacements(text, replacements));
        }

        [Fact]
        public void TestOutOfRangeReplacementThrows()
        {
            var text = "Test";
            
            var replacements = new[] {
                new TextReplacement() {
                    Start = 8, // This replacement starts outside the text's length
                    OriginalText = "Test", 
                    ReplacementText = ""
                },                
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => LanguageUpgrader.ApplyReplacements(text, replacements));
        }
    }
}
