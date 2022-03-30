using Xunit;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{
    public class TagTests : TestBase
    {
        public TagTests() : base()
        {
        }

        [Fact]
        void TestNoOptionsLineNotTagged()
        {
            var source = "title:Start\n---\nline without options #line:1\n===\n";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);

            var info = result.StringTable["line:1"];

            Assert.DoesNotContain("lastline",info.metadata);
        }

        [Fact]
        void TestLineBeforeOptionsTaggedLastLine()
        {
            var source = "title:Start\n---\nline before options #line:1\n-> option 1\n-> option 2\n===\n";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);

            var info = result.StringTable["line:1"];

            Assert.Contains("lastline",info.metadata);
        }

        [Fact]
        void TestLineNotBeforeOptionsNotTaggedLastLine()
        {
            var source = "title:Start\n---\nline not before options #line:0\nline before options #line:1\n-> option 1\n-> option 2\n===\n";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);

            var info = result.StringTable["line:0"];

            Assert.DoesNotContain("lastline", info.metadata);
        }

        [Fact]
        void TestLineAfterOptionsNotTaggedLastLine()
        {
            var source = "title:Start\n---\nline before options #line:1\n-> option 1\n-> option 2\nline after options #line:2\n===\n";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);

            var info = result.StringTable["line:2"];

            Assert.DoesNotContain("lastline", info.metadata);
        }

        [Fact]
        void TestNestedOptionLinesTaggedLastLine()
        {
            var source = CreateTestNode(@"
line before options #line:1
-> option 1
    line 1a #line:1a
    line 1b #line:1b
    -> option 1a
    -> option 1b
-> option 2
-> option 3
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);
            var info = result.StringTable["line:1"];
            Assert.Contains("lastline", info.metadata);

            info = result.StringTable["line:1b"];
            Assert.Contains("lastline", info.metadata);
        }

        [Fact]
        void TestIfInteriorLinesTaggedLastLine()
        {
            var source = CreateTestNode(@"
<<if true>>
line before options #line:0
-> option 1
-> option 2
<<endif>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);
            var info = result.StringTable["line:0"];
            Assert.Contains("lastline", info.metadata);
        }
        [Fact]
        void TestIfInteriorLinesNotTaggedLastLine()
        {
            var source = CreateTestNode(@"
<<if true>>
line before options #line:0
<<endif>>
-> option 1
-> option 2
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);
            var info = result.StringTable["line:0"];
            Assert.DoesNotContain("lastline", info.metadata);
        }

        [Fact]
        void TestNestedOptionLinesNotTagged()
        {
            var source = CreateTestNode(@"
-> option 1
    inside options #line:1a
-> option 2
-> option 3
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);

            var info = result.StringTable["line:1a"];
            Assert.DoesNotContain("lastline", info.metadata);
        }

        [Fact]
        void TestInterruptedLinesNotTagged()
        {
            var source = CreateTestNode(@"
line before command #line:0
<<custom command>>
-> option 1
line before declare #line:1
<<declare $value = 0>>
-> option 1
line before set #line:2
<<set $value = 0>>
-> option 1
line before jump #line:3
<<jump nodename>>
line before call #line:4
<<call function()>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            Assert.Empty(result.Diagnostics);

            var info = result.StringTable["line:0"];
            Assert.DoesNotContain("lastline", info.metadata);
            info = result.StringTable["line:1"];
            Assert.DoesNotContain("lastline", info.metadata);
            info = result.StringTable["line:2"];
            Assert.DoesNotContain("lastline", info.metadata);
            info = result.StringTable["line:3"];
            Assert.DoesNotContain("lastline", info.metadata);
            info = result.StringTable["line:4"];
            Assert.DoesNotContain("lastline", info.metadata);
        }
    }
}
