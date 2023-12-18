using Xunit;
using Yarn.Compiler;
using FluentAssertions;

namespace YarnSpinner.Tests
{
    public class TagTests : TestBase
    {
        public TagTests() : base()
        {
        }

        [Fact]
        public void TestNoOptionsLineNotTagged()
        {
            var source = @"title:Start
---
line without options #line:1
===
";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();

            var info = result.StringTable["line:1"];

            info.metadata.Should().NotContain("lastline");
        }

        [Fact]
        public void TestLineBeforeOptionsTaggedLastLine()
        {
            var source = @"title:Start
---
line before options #line:1
-> option 1
-> option 2
===
";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();

            var info = result.StringTable["line:1"];

            info.metadata.Should().Contain("lastline");
        }

        [Fact]
        public void TestLineNotBeforeOptionsNotTaggedLastLine()
        {
            var source = @"title:Start
---
line not before options #line:0
line before options #line:1
-> option 1
-> option 2
===
";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();

            var info = result.StringTable["line:0"];

            info.metadata.Should().NotContain("lastline");
        }

        [Fact]
        public void TestLineAfterOptionsNotTaggedLastLine()
        {
            var source = @"title:Start
---
line before options #line:1
-> option 1
-> option 2
line after options #line:2
===
";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();

            var info = result.StringTable["line:2"];

            info.metadata.Should().NotContain("lastline");
        }

        [Fact]
        public void TestNestedOptionLinesTaggedLastLine()
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
            result.Diagnostics.Should().BeEmpty();
            var info = result.StringTable["line:1"];
            info.metadata.Should().Contain("lastline");

            info = result.StringTable["line:1b"];
            info.metadata.Should().Contain("lastline");
        }

        [Fact]
        public void TestIfInteriorLinesTaggedLastLine()
        {
            var source = CreateTestNode(@"
<<if true>>
line before options #line:0
-> option 1
-> option 2
<<endif>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();
            var info = result.StringTable["line:0"];
            info.metadata.Should().Contain("lastline");
        }
        [Fact]
        public void TestIfInteriorLinesNotTaggedLastLine()
        {
            var source = CreateTestNode(@"
<<if true>>
line before options #line:0
<<endif>>
-> option 1
-> option 2
");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();
            var info = result.StringTable["line:0"];
            info.metadata.Should().NotContain("lastline");
        }

        [Fact]
        public void TestNestedOptionLinesNotTagged()
        {
            var source = CreateTestNode(@"
-> option 1
    inside options #line:1a
-> option 2
-> option 3
");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();

            var info = result.StringTable["line:1a"];
            info.metadata.Should().NotContain("lastline");
        }

        [Fact]
        public void TestInterruptedLinesNotTagged()
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
<<call string(5)>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();

            var info = result.StringTable["line:0"];
            info.metadata.Should().NotContain("lastline");
            info = result.StringTable["line:1"];
            info.metadata.Should().NotContain("lastline");
            info = result.StringTable["line:2"];
            info.metadata.Should().NotContain("lastline");
            info = result.StringTable["line:3"];
            info.metadata.Should().NotContain("lastline");
            info = result.StringTable["line:4"];
            info.metadata.Should().NotContain("lastline");
        }

        [Fact]
        public void TestLineIsLastBeforeAnotherNodeNotTagged()
        {
            var source = @"title: Start
---
last line #line:0
===
title: Second
---
-> option 1
===
";
            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.Diagnostics.Should().BeEmpty();

            var info = result.StringTable["line:0"];
            info.metadata.Should().NotContain("lastline");
        }

        [Fact]
        public void TestCommentsArentTagged()
        {
            var escapedText = @"title: Start
---
\\
===";
            // ensuring the base text compiles fine as is
            var job = CompilationJob.CreateFromString("input", escapedText);
            job.CompilationType = CompilationJob.Type.StringsOnly;
            var results = Compiler.Compile(job);
            results.Diagnostics.Should().BeEmpty();

            // tagging the line
            var tagged = Utility.TagLines(escapedText);
            var taggedVersion = tagged.Item1;

            // recompiling, we should have no errors
            job = CompilationJob.CreateFromString("input", taggedVersion);
            job.CompilationType = CompilationJob.Type.StringsOnly;
            results = Compiler.Compile(job);
            results.Diagnostics.Should().BeEmpty();
        }
    }
}
