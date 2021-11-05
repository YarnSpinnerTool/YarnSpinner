using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;
using Yarn.Compiler;
using Yarn.Markup;

namespace YarnSpinner.Tests
{

    public class MarkupTests : TestBase
    {

        [Fact]
        public void TestMarkupParsing() {
            var line = "A [b]B[/b]";
            var markup = dialogue.ParseMarkup(line);
            
            Assert.Equal("A B", markup.Text);
            Assert.Single(markup.Attributes);
            Assert.Equal("b", markup.Attributes[0].Name);
            Assert.Equal(2, markup.Attributes[0].Position);
            Assert.Equal(1, markup.Attributes[0].Length);
        }

        [Fact]
        public void TestOverlappingAttributes() {
            var line = "[a][b][c]X[/b][/a]X[/c]";

            var markup = dialogue.ParseMarkup(line);

            Assert.Equal(3, markup.Attributes.Count);
            Assert.Equal("a", markup.Attributes[0].Name);
            Assert.Equal("b", markup.Attributes[1].Name);
            Assert.Equal("c", markup.Attributes[2].Name);

        }

        [Fact]
        public void TestTextExtraction() {
            var line = "A [b]B [c]C[/c][/b]";

            var markup = dialogue.ParseMarkup(line);

            Assert.Equal("B C", markup.TextForAttribute(markup.Attributes[0]));
            Assert.Equal("C", markup.TextForAttribute(markup.Attributes[1]));
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
            Assert.Equal("X", originalMarkup.Attributes[3].Name);
            var trimmedMarkup = originalMarkup.DeleteRange(originalMarkup.Attributes[3]);
            
            Assert.Equal("A x x B C", originalMarkup.Text);
            Assert.Equal(6, originalMarkup.Attributes.Count);

            Assert.Equal("A  B C", trimmedMarkup.Text);
            Assert.Equal(4, trimmedMarkup.Attributes.Count);
            
            Assert.Equal("a", trimmedMarkup.Attributes[0].Name);
            Assert.Equal(0, trimmedMarkup.Attributes[0].Position);
            Assert.Equal(6, trimmedMarkup.Attributes[0].Length);

            Assert.Equal("b", trimmedMarkup.Attributes[1].Name);
            Assert.Equal(0, trimmedMarkup.Attributes[1].Position);
            Assert.Equal(2, trimmedMarkup.Attributes[1].Length);

            // "c" will have been removed along with "X" because it had a
            // length of >0 before deletion, and was reduced to zero
            // characters

            Assert.Equal("d", trimmedMarkup.Attributes[2].Name);
            Assert.Equal(2, trimmedMarkup.Attributes[2].Position);
            Assert.Equal(2, trimmedMarkup.Attributes[2].Length);

            Assert.Equal("e", trimmedMarkup.Attributes[3].Name);
            Assert.Equal(5, trimmedMarkup.Attributes[3].Position);
            Assert.Equal(1, trimmedMarkup.Attributes[3].Length);

        }

        [Fact]
        public void TestFindingAttributes()
        {
            var line = "A [b]B[/b] [b]C[/b]";
            var markup = dialogue.ParseMarkup(line);

            MarkupAttribute attribute;
            bool found;
            
            found = markup.TryGetAttributeWithName("b", out attribute);

            Assert.True(found);
            Assert.Equal(attribute, markup.Attributes[0]);
            Assert.NotEqual(attribute, markup.Attributes[1]);

            found = markup.TryGetAttributeWithName("c", out _);

            Assert.False(found);                        
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
            Assert.Single(markup.Attributes);
            Assert.Equal(2, markup.Attributes[0].Position);
            Assert.Equal(1, markup.Attributes[0].Length);
        }

        [Theory]
        [InlineData("[a][/a][/b]")]
        [InlineData("[/b]")]
        [InlineData("[a][/][/b]")]
        public void TestUnexpectedCloseMarkerThrows(string input) {
            Assert.Throws<MarkupParseException>(delegate {
                var markup = dialogue.ParseMarkup(input);
            });            
        }

        [Fact]
        public void TestMarkupShortcutPropertyParsing() {
            var line = "[a=1]s[/a]";
            var markup = dialogue.ParseMarkup(line);

            // Should have a single attribute, "a", at position 0 and
            // length 1
            var attribute = markup.Attributes[0];
            Assert.Equal("a", attribute.Name);
            Assert.Equal(0, attribute.Position);
            Assert.Equal(1, attribute.Length);

            // Should have a single property on this attribute, "a". Value
            // should be an integer, 1
            var value = attribute.Properties["a"];
            
            Assert.Equal(MarkupValueType.Integer, value.Type);
            Assert.Equal(1, value.IntegerValue);
        }

        [Fact]
        public void TestMarkupMultiplePropertyParsing() {
            var line = "[a p1=1 p2=2]s[/a]";
            var markup = dialogue.ParseMarkup(line);

            Assert.Equal("a", markup.Attributes[0].Name);
            
            Assert.Equal(2, markup.Attributes[0].Properties.Count);

            var p1 = markup.Attributes[0].Properties["p1"];
            Assert.Equal(MarkupValueType.Integer, p1.Type);
            Assert.Equal(1, p1.IntegerValue);

            var p2 = markup.Attributes[0].Properties["p2"];
            Assert.Equal(MarkupValueType.Integer, p2.Type);
            Assert.Equal(2, p2.IntegerValue);
            
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

            Assert.Equal(expectedType, propertyValue.Type);
            Assert.Equal(expectedValueAsString, propertyValue.ToString());
        }

        [Theory]
        [InlineData("A [b]B [c]C[/c][/b] D")] // attributes can be closed
        [InlineData("A [b]B [c]C[/b][/c] D")] // attributes can be closed out of order
        [InlineData("A [b]B [c]C[/] D")] // "[/]" closes all open attributes
        public void TestMultipleAttributes(string input) {
            var markup = dialogue.ParseMarkup(input);

            Assert.Equal("A B C D", markup.Text);

            Assert.Equal(2, markup.Attributes.Count);

            Assert.Equal("b", markup.Attributes[0].Name);
            Assert.Equal(2, markup.Attributes[0].Position);
            Assert.Equal(2, markup.Attributes[0].SourcePosition);
            Assert.Equal(3, markup.Attributes[0].Length);

            Assert.Equal("c", markup.Attributes[1].Name);
            Assert.Equal(4, markup.Attributes[1].Position);
            Assert.Equal(7, markup.Attributes[1].SourcePosition);
            Assert.Equal(1, markup.Attributes[1].Length);
        }

        [Fact]
        public void TestSelfClosingAttributes() {
            var line = "A [a/] B";
            var markup = dialogue.ParseMarkup(line);

            Assert.Equal("A B", markup.Text);

            Assert.Single(markup.Attributes);

            Assert.Equal("a", markup.Attributes[0].Name);
            Assert.Equal(0, markup.Attributes[0].Properties.Count);
            Assert.Equal(2, markup.Attributes[0].Position);
            Assert.Equal(0, markup.Attributes[0].Length);
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

            Assert.Equal(expectedText, markup.Text);
        }

        [Theory]
        // character attribute can be implicit
        [InlineData("Mae: Wow!")] 
        // character attribute can also be explicit
        [InlineData("[character name=\"Mae\"]Mae: [/character]Wow!")] 
        public void TestImplicitCharacterAttributeParsing(string input) {
            var markup = dialogue.ParseMarkup(input);

            Assert.Equal("Mae: Wow!", markup.Text);
            Assert.Single(markup.Attributes);

            Assert.Equal("character", markup.Attributes[0].Name);
            Assert.Equal(0, markup.Attributes[0].Position);
            Assert.Equal(5, markup.Attributes[0].Length);

            Assert.Equal(1, markup.Attributes[0].Properties.Count);
            Assert.Equal("Mae", markup.Attributes[0].Properties["name"].StringValue);
        }

        [Fact]
        public void TestNoMarkupModeParsing() {
            var line = "S [a]S[/a] [nomarkup][a]S;][/a][/nomarkup]";
            var markup = dialogue.ParseMarkup(line);

            Assert.Equal("S S [a]S;][/a]", markup.Text);

            Assert.Equal(2, markup.Attributes.Count);

            Assert.Equal("a", markup.Attributes[0].Name);
            Assert.Equal(2, markup.Attributes[0].Position);
            Assert.Equal(1, markup.Attributes[0].Length);

            Assert.Equal("nomarkup", markup.Attributes[1].Name);
            Assert.Equal(4, markup.Attributes[1].Position);
            Assert.Equal(10, markup.Attributes[1].Length);
        }

        [Fact]
        public void TestMarkupEscaping() {
            var line = @"[a]hello \[b\]hello\[/b\][/a]";
            var markup = dialogue.ParseMarkup(line);

            Assert.Equal("hello [b]hello[/b]", markup.Text);
            Assert.Single(markup.Attributes);
            Assert.Equal("a", markup.Attributes[0].Name);
            Assert.Equal(0, markup.Attributes[0].Position);
            Assert.Equal(18, markup.Attributes[0].Length);
        }
    }
}
