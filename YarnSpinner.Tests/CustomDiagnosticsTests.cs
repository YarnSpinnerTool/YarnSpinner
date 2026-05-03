using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;


namespace YarnSpinner.Tests
{


    public class CustomDiagnosticsTests : TestBase
    {
        public CustomDiagnosticsTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

        [Fact]
        public void TestCustomDiagnosticsProvidersCanBeProvided()
        {
            // Given
            var source = """
            title: Start
            ---
            Here's a long line. It's really long. Too long for most, some would say.
            ===
            """;

            var job = CompilationJob.CreateFromString("MyCoolFile.yarn", source);
            job.CustomDiagnosticProviders = [
                new LineTooLongDiagnosticsProvider(60),
                new LineMustHaveSpecialHashtagProvider()
            ];

            // When
            var result = Compiler.Compile(job);

            // Then
            var lineTooLongDiag = result.Diagnostics.Should().Contain(d => d.Message == "Line is longer than 60 characters").Subject;
            lineTooLongDiag.Code.Should().Be(DiagnosticDescriptor.CustomDiagnostic.Code);
            lineTooLongDiag.FileName.Should().Be("MyCoolFile.yarn");
            lineTooLongDiag.Range.Start.Line.Should().Be(2);
            lineTooLongDiag.Range.Start.Character.Should().Be(0);
            lineTooLongDiag.Range.Start.Line.Should().Be(2);
            lineTooLongDiag.Range.End.Character.Should().Be(73);
            lineTooLongDiag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);

            var missingHashtagDiag = result.Diagnostics.Should().Contain(d => d.Message == "Line is missing #test").Subject;
            missingHashtagDiag.FileName.Should().Be("MyCoolFile.yarn");
            missingHashtagDiag.Range.Start.Line.Should().Be(2);
            missingHashtagDiag.Range.Start.Character.Should().Be(0);
            missingHashtagDiag.Range.Start.Line.Should().Be(2);
            missingHashtagDiag.Range.End.Character.Should().Be(73);
            missingHashtagDiag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);
        }
    }

    class LineMustHaveSpecialHashtagProvider : ICustomDiagnosticProvider
    {
        public void ProvideDiagnostics(IBuildContext context)
        {
            foreach (var stmt in context.LineStatements)
            {
                if (!stmt.Hashtags.Contains("test"))
                {
                    context.EmitDiagnostic(stmt, "Line is missing #test");
                }
            }
        }
    }

    class LineTooLongDiagnosticsProvider : ICustomDiagnosticProvider
    {
        public LineTooLongDiagnosticsProvider(int maxLength)
        {
            if (maxLength <= 0)
            {
                throw new System.ArgumentException("Minimum line length can't be less than 1");
            }
            this.MaxLength = maxLength;
        }

        public int MaxLength { get; }

        public void ProvideDiagnostics(IBuildContext context)
        {
            foreach (var statement in context.LineStatements)
            {
                if (statement.LineText.Length > MaxLength)
                {
                    context.EmitDiagnostic(statement, $"Line is longer than {MaxLength} characters", Diagnostic.DiagnosticSeverity.Warning);
                }
            }
        }
    }
}

