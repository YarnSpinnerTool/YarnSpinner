using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;
using Yarn.Compiler;
using Yarn.Markup;
using FluentAssertions;

namespace YarnSpinner.Tests
{

    public class MarkupTests : TestBase
    {

        [Fact]
        public void TestMarkupParsing() {
            var line = "A [b]B[/b]";
            var markup = dialogue.ParseMarkup(line);
            
            markup.Text.Should().Be("A B");
            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Name.Should().Be("b");
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(1);
        }

        [Fact]
        public void TestOverlappingAttributes() {
            var line = "[a][b][c]X[/b][/a]X[/c]";

            var markup = dialogue.ParseMarkup(line);

            markup.Attributes.Count.Should().Be(3);
            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[1].Name.Should().Be("b");
            markup.Attributes[2].Name.Should().Be("c");

        }

        [Fact]
        public void TestTextExtraction() {
            var line = "A [b]B [c]C[/c][/b]";

            var markup = dialogue.ParseMarkup(line);

            markup.TextForAttribute(markup.Attributes[0]).Should().Be("B C");
            markup.TextForAttribute(markup.Attributes[1]).Should().Be("C");
        }

        [Fact]
        public void TestAttributeRemoval() {
            // A test string with the following attributes:
            // a: Covers the entire string
            // b: Starts outside X, ends inside
            // c: Same start and end point as X
            // d: Starts inside X, ends outside
            // e: Starts and ends outside X
            var line = "[a][b]A [c][X]x[/b] [d]x[/X][/c] B[/d] [e]C[/e][/a]";
            var originalMarkup = dialogue.ParseMarkup(line);

            // Remove the "X" attribute
            originalMarkup.Attributes[3].Name.Should().Be("X");
            var trimmedMarkup = originalMarkup.DeleteRange(originalMarkup.Attributes[3]);
            
            originalMarkup.Text.Should().Be("A x x B C");
            originalMarkup.Attributes.Count.Should().Be(6);

            trimmedMarkup.Text.Should().Be("A  B C");
            trimmedMarkup.Attributes.Count.Should().Be(4);
            
            trimmedMarkup.Attributes[0].Name.Should().Be("a");
            trimmedMarkup.Attributes[0].Position.Should().Be(0);
            trimmedMarkup.Attributes[0].Length.Should().Be(6);

            trimmedMarkup.Attributes[1].Name.Should().Be("b");
            trimmedMarkup.Attributes[1].Position.Should().Be(0);
            trimmedMarkup.Attributes[1].Length.Should().Be(2);

            // "c" will have been removed along with "X" because it had a
            // length of >0 before deletion, and was reduced to zero
            // characters

            trimmedMarkup.Attributes[2].Name.Should().Be("d");
            trimmedMarkup.Attributes[2].Position.Should().Be(2);
            trimmedMarkup.Attributes[2].Length.Should().Be(2);

            trimmedMarkup.Attributes[3].Name.Should().Be("e");
            trimmedMarkup.Attributes[3].Position.Should().Be(5);
            trimmedMarkup.Attributes[3].Length.Should().Be(1);

        }

        [Fact]
        public void TestFindingAttributes()
        {
            var line = "A [b]B[/b] [b]C[/b]";
            var markup = dialogue.ParseMarkup(line);

            MarkupAttribute attribute;
            bool found;
            
            found = markup.TryGetAttributeWithName("b", out attribute);

            found.Should().BeTrue();
            markup.Attributes[0].Should().Be(attribute);
            markup.Attributes[1].Should().NotBe(attribute);

            found = markup.TryGetAttributeWithName("c", out _);

            found.Should().BeFalse();                        
        }

        [Theory]
        [InlineData("á [á]S[/á]")]
        [InlineData("á [a]á[/a]")]
        [InlineData("á [a]S[/a]")]
        [InlineData("S [á]S[/á]")]
        [InlineData("S [a]á[/a]")]
        [InlineData("S [a]S[/a]")]
        public void TestMultibyteCharacterParsing(string input) {
            var markup = dialogue.ParseMarkup(input);

            // All versions of this string should have the same position
            // and length of the attribute, despite the presence of
            // multibyte characters
            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(1);
        }

        [Theory]
        [InlineData("[a][/a][/b]")]
        [InlineData("[/b]")]
        [InlineData("[a][/][/b]")]
        public void TestUnexpectedCloseMarkerThrows(string input) {
            var parsingInvalidMarkup = new Action(() => 
            {
                var markup = dialogue.ParseMarkup(input);
            });

            parsingInvalidMarkup.Should().Throw<MarkupParseException>();
        }

        [Fact]
        public void TestMarkupShortcutPropertyParsing() {
            var line = "[a=1]s[/a]";
            var markup = dialogue.ParseMarkup(line);

            // Should have a single attribute, "a", at position 0 and
            // length 1
            var attribute = markup.Attributes[0];
            attribute.Name.Should().Be("a");
            attribute.Position.Should().Be(0);
            attribute.Length.Should().Be(1);

            // Should have a single property on this attribute, "a". Value
            // should be an integer, 1
            var value = attribute.Properties["a"];
            
            value.Type.Should().Be(MarkupValueType.Integer);
            value.IntegerValue.Should().Be(1);
        }

        [Fact]
        public void TestMarkupMultiplePropertyParsing() {
            var line = "[a p1=1 p2=2]s[/a]";
            var markup = dialogue.ParseMarkup(line);

            markup.Attributes[0].Name.Should().Be("a");
            
            markup.Attributes[0].Properties.Count.Should().Be(2);

            var p1 = markup.Attributes[0].Properties["p1"];
            p1.Type.Should().Be(MarkupValueType.Integer);
            p1.IntegerValue.Should().Be(1);

            var p2 = markup.Attributes[0].Properties["p2"];
            p2.Type.Should().Be(MarkupValueType.Integer);
            p2.IntegerValue.Should().Be(2);
            
        }

        [Theory]
        [InlineData(@"[a p=""string""]s[/a]", MarkupValueType.String, "string")]
        [InlineData(@"[a p=""str\""ing""]s[/a]", MarkupValueType.String, @"str""ing")]
        [InlineData("[a p=string]s[/a]", MarkupValueType.String, "string")]
        [InlineData("[a p=42]s[/a]", MarkupValueType.Integer, "42")]
        [InlineData("[a p=13.37]s[/a]", MarkupValueType.Float, "13.37")]
        [InlineData("[a p=true]s[/a]", MarkupValueType.Bool, "True")]
        [InlineData("[a p=false]s[/a]", MarkupValueType.Bool, "False")]
        public void TestMarkupPropertyParsing(string input, MarkupValueType expectedType, string expectedValueAsString) {
            var markup = dialogue.ParseMarkup(input);

            var attribute = markup.Attributes[0];
            var propertyValue= attribute.Properties["p"];

            propertyValue.Type.Should().Be(expectedType);
            propertyValue.ToString().Should().Be(expectedValueAsString);
        }

        [Theory]
        [InlineData("A [b]B [c]C[/c][/b] D")] // attributes can be closed
        [InlineData("A [b]B [c]C[/b][/c] D")] // attributes can be closed out of order
        [InlineData("A [b]B [c]C[/] D")] // "[/]" closes all open attributes
        public void TestMultipleAttributes(string input) {
            var markup = dialogue.ParseMarkup(input);

            markup.Text.Should().Be("A B C D");

            markup.Attributes.Count.Should().Be(2);

            markup.Attributes[0].Name.Should().Be("b");
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].SourcePosition.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(3);

            markup.Attributes[1].Name.Should().Be("c");
            markup.Attributes[1].Position.Should().Be(4);
            markup.Attributes[1].SourcePosition.Should().Be(7);
            markup.Attributes[1].Length.Should().Be(1);
        }

        [Fact]
        public void TestSelfClosingAttributes() {
            var line = "A [a/] B";
            var markup = dialogue.ParseMarkup(line);

            markup.Text.Should().Be("A B");

            markup.Attributes.Should().ContainSingle();

            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[0].Properties.Count.Should().Be(0);
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(0);
        }

        [Theory]
        [InlineData("A [a/] B", "A B")]
        [InlineData("A [a trimwhitespace=true/] B", "A B")]
        [InlineData("A [a trimwhitespace=false/] B", "A  B")]
        [InlineData("A [nomarkup/] B", "A  B")]
        [InlineData("A [nomarkup trimwhitespace=false/] B", "A  B")]
        [InlineData("A [nomarkup trimwhitespace=true/] B", "A B")]
        public void TestAttributesMayTrimTrailingWhitespace(string input, string expectedText) {
            var markup = dialogue.ParseMarkup(input);

            markup.Text.Should().Be(expectedText);
        }

        [Theory]
        // character attribute can be implicit
        [InlineData("Mae: Wow!")] 
        // character attribute can also be explicit
        [InlineData("[character name=\"Mae\"]Mae: [/character]Wow!")] 
        public void TestImplicitCharacterAttributeParsing(string input) {
            var markup = dialogue.ParseMarkup(input);

            markup.Text.Should().Be("Mae: Wow!");
            markup.Attributes.Should().ContainSingle();

            markup.Attributes[0].Name.Should().Be("character");
            markup.Attributes[0].Position.Should().Be(0);
            markup.Attributes[0].Length.Should().Be(5);

            markup.Attributes[0].Properties.Count.Should().Be(1);
            markup.Attributes[0].Properties["name"].StringValue.Should().Be("Mae");
        }

        [Fact]
        public void TestNoMarkupModeParsing() {
            var line = "S [a]S[/a] [nomarkup][a]S;][/a][/nomarkup]";
            var markup = dialogue.ParseMarkup(line);

            markup.Text.Should().Be("S S [a]S;][/a]");

            markup.Attributes.Count.Should().Be(2);

            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(1);

            markup.Attributes[1].Name.Should().Be("nomarkup");
            markup.Attributes[1].Position.Should().Be(4);
            markup.Attributes[1].Length.Should().Be(10);
        }

        [Fact]
        public void TestMarkupEscaping() {
            var line = @"[a]hello \[b\]hello\[/b\][/a]";
            var markup = dialogue.ParseMarkup(line);

            markup.Text.Should().Be("hello [b]hello[/b]");
            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[0].Position.Should().Be(0);
            markup.Attributes[0].Length.Should().Be(18);
        }

        [Fact]
        public void TestNumericProperties() {
            var line = @"[select value=1 1=one 2=two 3=three /]";
            var markup = dialogue.ParseMarkup(line);

            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Name.Should().Be("select");
            markup.Attributes[0].Properties.Count.Should().Be(4);
            markup.Attributes[0].Properties["value"].IntegerValue.Should().Be(1);
            markup.Attributes[0].Properties["1"].StringValue.Should().Be("one");
            markup.Attributes[0].Properties["2"].StringValue.Should().Be("two");
            markup.Attributes[0].Properties["3"].StringValue.Should().Be("three");

            markup.Text.Should().Be("one");
        }

        [Fact]
        public void TestNumberPluralisation() {

            var testCases = new[] {
                (Value: 1, Locale: "en", Expected: "a single cat"),
                (Value: 2, Locale: "en", Expected: "2 cats"),
                (Value: 3, Locale: "en", Expected: "3 cats"),
                (Value: 1, Locale: "en-AU", Expected: "a single cat"),
                (Value: 2, Locale: "en-AU", Expected: "2 cats"),
                (Value: 3, Locale: "en-AU", Expected: "3 cats"),
            };

            using (new FluentAssertions.Execution.AssertionScope())
            {

                foreach (var testCase in testCases)
                {
                    var line = "[plural value=" + testCase.Value + " one=\"a single cat\" other=\"% cats\"/]";

                    dialogue.LanguageCode = testCase.Locale;
                    var markup = dialogue.ParseMarkup(line);
                    markup.Text.Should().Be(testCase.Expected, $"{testCase.Value} in locale {testCase.Locale} should have the correct plural case");
                }
            }

        }
    }
}
