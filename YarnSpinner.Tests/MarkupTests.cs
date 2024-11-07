using Xunit;
using System;
using System.Collections.Generic;
using Yarn.Markup;
using FluentAssertions;
using Xunit.Abstractions;
using System.Text;

namespace YarnSpinner.Tests
{
    public class MarkupTests : TestBase, IAttributeMarkerProcessor
    {
        public MarkupTests(ITestOutputHelper outputHelper) : base(outputHelper) { }
        
        [Fact]
        public void TestMarkupParsing()
        {
            var line = "A [b]B[/b]";
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");
            
            markup.Text.Should().Be("A B");
            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Name.Should().Be("b");
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(1);
        }

        private LineParser.MarkupTreeNode descendant(LineParser.MarkupTreeNode root, int[] children)
        {
            var current = root;
            foreach (var child in children)
            {
                current.children.Should().HaveCountGreaterThan(child);
                current = current.children[child];
            }
            return current;
        }
        private LineParser.MarkupTreeNode Descendant(LineParser.MarkupTreeNode root, params int[] children)
        {
            return descendant(root, children);
        }

        [Theory]
        [InlineData("this is a line with [markup]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text}, new string[] {"this is a line with ","[","markup","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup = 1]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.NumberValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","1","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup=12]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.NumberValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","12","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup = 12 ]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.NumberValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","12","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup = \"12\" ]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.StringValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","\"12\"","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup=\"12\"]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.StringValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","\"12\"","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup=hello]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.StringValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","hello","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup=true]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.BooleanValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","true","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup=false]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.BooleanValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","false","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup=false var = 12]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.BooleanValue,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.NumberValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","=","false","var","=","12","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [markup=false var = 12]two [markup2]markup[/] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.BooleanValue,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.NumberValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,},new string[]{"this is a line with ","[","markup","=","false","var","=","12","]","two ","[","markup2","]","markup","[","/","]"," inside of it"})]
        [InlineData("this is a line with \\[markup=false var = 12]two [markup2]markup[/] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with \\[markup=false var = 12]two ","[","markup2","]","markup","[","/","]"," inside of it"})]
        [InlineData("this is a line with [markup markup = 1]a single markup[/markup] inside of it",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.NumberValue,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text},new string[]{"this is a line with ","[","markup","markup","=","1","]","a single markup","[","/","markup","]"," inside of it"})]
        [InlineData("this is a line with [interpolated markup = {$property} /] inside",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.Equals,LineParser.LexerTokenTypes.InterpolatedValue,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,},new string[]{"this is a line with ", "[", "interpolated", "markup", "=", "{$property}", "/", "]", " inside"})]
        [InlineData("á [a]S[/a]",new LineParser.LexerTokenTypes[]{LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,LineParser.LexerTokenTypes.Text,LineParser.LexerTokenTypes.OpenMarker,LineParser.LexerTokenTypes.CloseSlash,LineParser.LexerTokenTypes.Identifier,LineParser.LexerTokenTypes.CloseMarker,},new string[]{"á ", "[", "a", "]", "S", "[", "/", "a", "]"})]
        void TestLexerGeneratesCorrectTokens(string line, LineParser.LexerTokenTypes[] types, string[] texts)
        {
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);

            line = line.Normalize();
            
            // removing the start and end tokens
            tokens.RemoveAt(0);
            tokens.RemoveAt(tokens.Count - 1);

            // we should have the same number of tokens as the expected token stream
            tokens.Should().HaveCount(types.Length);
            for (int i = 0; i < types.Length; i++)
            {
                tokens[i].type.Should().Be(types[i]);
            }

            // we should have the same number and values of text as the tokens express in their range
            tokens.Should().HaveCount(texts.Length);
            for (int i = 0; i < texts.Length; i++)
            {
                var token = tokens[i];
                var text = line.Substring(token.start, token.range);
                var comparison = texts[i].Normalize();
                text.Should().BeEquivalentTo(comparison);
            }
        }

        [Fact] void TestNoMarkupInLexerConsumesTokens()
        {
            var line = "this is a line with [nomarkup]bunch[ /] a = 2 of \" [tag /] [anothertag]invalid shit[/anothertag] yes[/nomarkup]";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);

            // removing the start and end tokens
            tokens.RemoveAt(0);
            tokens.RemoveAt(tokens.Count - 1);

            LineParser.LexerTokenTypes[] types = {LineParser.LexerTokenTypes.Text,
                                                  LineParser.LexerTokenTypes.OpenMarker,
                                                  LineParser.LexerTokenTypes.Identifier,
                                                  LineParser.LexerTokenTypes.CloseMarker,
                                                  LineParser.LexerTokenTypes.Text,
                                                  LineParser.LexerTokenTypes.OpenMarker,
                                                  LineParser.LexerTokenTypes.CloseSlash,
                                                  LineParser.LexerTokenTypes.CloseMarker,
                                                  LineParser.LexerTokenTypes.Text,
                                                  LineParser.LexerTokenTypes.OpenMarker,
                                                  LineParser.LexerTokenTypes.Identifier,
                                                  LineParser.LexerTokenTypes.CloseSlash,
                                                  LineParser.LexerTokenTypes.CloseMarker,
                                                  LineParser.LexerTokenTypes.Text,
                                                  LineParser.LexerTokenTypes.OpenMarker,
                                                  LineParser.LexerTokenTypes.Identifier,
                                                  LineParser.LexerTokenTypes.CloseMarker,
                                                  LineParser.LexerTokenTypes.Text,
                                                  LineParser.LexerTokenTypes.OpenMarker,
                                                  LineParser.LexerTokenTypes.CloseSlash,
                                                  LineParser.LexerTokenTypes.Identifier,
                                                  LineParser.LexerTokenTypes.CloseMarker,
                                                  LineParser.LexerTokenTypes.Text,
                                                  LineParser.LexerTokenTypes.OpenMarker,
                                                  LineParser.LexerTokenTypes.CloseSlash,
                                                  LineParser.LexerTokenTypes.Identifier,
                                                  LineParser.LexerTokenTypes.CloseMarker,
                                                 };
            tokens.Should().HaveCount(types.Length);
            for (int i = 0; i < types.Length; i++)
            {
                tokens[i].type.Should().Be(types[i]);
            }
        }

        [Fact]
        public void TestUnsquishedTreeWithSingleChildIsValid()
        {
            var line = "this is a line with [markup]a single markup[/markup] inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;

            // tree
            //      text
            //      attribute
            //          text
            //      text
            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(1);
            errors.Should().BeEmpty();
        }
        [Fact]
        public void TestUnsquishedTreeWithSelfCloseMarkupIsValid()
        {
            var line = "this is a line with [markup /]a single self-closing markup inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;

            // tree
            //      text
            //      attribute
            //          text
            //      text
            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(0);
            errors.Should().BeEmpty();
        }
        [Fact]
        public void TestUnsquishedTreeWithNestedMarkupIsValid()
        {
            var line = "this is a line with [markup]a [inner]nested[/inner] markup[/markup] inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;

            // tree
            //      text "this is a line with "
            //      markup
            //          text "a "
            //          inner
            //              text "nested"
            //          text " markup"
            //      text
            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(3);
            Descendant(tree, 1,1).children.Should().HaveCount(1);
            Descendant(tree, 1,1,0).Should().BeOfType(typeof(Yarn.Markup.LineParser.MarkupTextNode));
            errors.Should().BeEmpty();
        }
        [Fact]
        public void TestUnsquishedTreeWithSingleChildAndSelfPropertiesIsValid()
        {
            var line = "this is a line with [markup = 1]a single markup[/markup] inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            // tree
            //      text
            //      markup (markup = 1)
            //          text
            //      text
            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(1);
            tree.children[1].properties.Should().HaveCount(1);
            tree.children[1].properties[0].Value.IntegerValue.Should().Be(1);
            tree.children[1].properties[0].Name.Should().Be("markup");
        }
        [Fact]
        public void TestUnsquishedTreeWithSingleChildAndNonselfPropertyIsValid()
        {
            var line = "this is a line with [markup markup = 1]a single markup[/markup] inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            // tree
            //      text
            //      markup (markup = 1)
            //          text
            //      text
            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(1);
            tree.children[1].properties.Should().HaveCount(1);
            tree.children[1].properties[0].Value.IntegerValue.Should().Be(1);
            tree.children[1].properties[0].Name.Should().Be("markup");
        }
        [Fact]
        public void TestUnsquishedTreeWithSingleChildAndMultipleNonSelfPropertiesIsValid()
        {
            var line = "this is a line with [markup markup = 1 markup = 2]a single markup[/markup] inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            // tree
            //      text
            //      markup (markup = 1)
            //          text
            //          close markup
            //      text
            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(1);
            tree.children[1].properties.Should().HaveCount(2);
            tree.children[1].properties[0].Value.IntegerValue.Should().Be(1);
            tree.children[1].properties[0].Name.Should().Be("markup");
            tree.children[1].properties[1].Value.IntegerValue.Should().Be(2);
            tree.children[1].properties[1].Name.Should().Be("markup");
        }
        
        [Fact] public void TestUnsquishedTreeWithSingleChildAndMultipleNonSelfPropertiesOfMultipleTypesIsValid()
        {
            var line = "this is a line with [markup markup = 1 markup = markup markup = true markup = 1.1 markup = \"markup\"]a single markup[/markup] inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(1);
            tree.children[1].properties.Should().HaveCount(5);

            var properties = tree.children[1].properties;

            properties[0].Name.Should().Be("markup");
            properties[0].Value.IntegerValue.Should().Be(1);
            
            properties[1].Name.Should().Be("markup");
            properties[1].Value.StringValue.Should().Be("markup");

            properties[2].Name.Should().Be("markup");
            properties[2].Value.BoolValue.Should().Be(true);

            properties[3].Name.Should().Be("markup");
            properties[3].Value.FloatValue.Should().Be(1.1f);

            properties[4].Name.Should().Be("markup");
            properties[4].Value.StringValue.Should().Be("markup");
        }

        [Fact] public void TestUnsquishedTreeWithSelfClosingAndSelfPropertyIsValid()
        {
            var line = "this is a line with [markup = 1 /]a single self-closing markup inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(0);
            tree.children[1].properties.Should().HaveCount(2);
            tree.children[1].properties[0].Name.Should().Be("markup");
            tree.children[1].properties[0].Value.IntegerValue.Should().Be(1);
        }
        [Fact] public void TestUnsquishedTreeWithSelfClosingAndNonSelfPropertyIsValid()
        {
            var line = "this is a line with [markup markup = 1 /]a single self-closing markup inside of it";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            tree.children.Should().HaveCount(3);
            tree.children[1].children.Should().HaveCount(0);
            tree.children[1].properties.Should().HaveCount(2);
            tree.children[1].properties[0].Name.Should().Be("markup");
            tree.children[1].properties[0].Value.IntegerValue.Should().Be(1);
        }
        // need to test nomarkup
        [Fact] public void TestUnsquishedTreeWithNoMarkupAllowsInvalidCharacters()
        {
            var line = "this is a line with [nomarkup]bunch[ /] a = 2 of \" [tag /] [anothertag]invalid shit[/anothertag] yes[/nomarkup]";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            tree.children.Should().HaveCount(2);
            tree.children[1].children.Should().HaveCount(1); // text
            ((LineParser.MarkupTextNode)Descendant(tree,1,0)).text.Should().Be("bunch[ /] a = 2 of \" [tag /] [anothertag]invalid shit[/anothertag] yes");
        }
        [Fact] public void TestUnsquishedNestedMarkupIsValid()
        {
            var line = "This is [outer][inner]some [inmost /]nested[/inner][/outer] markup";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            tree.children.Should().HaveCount(3);
        }

        [Fact] public void TestUnsquishedImbalancedMarkupIsValid()
        {
            var line = "This [outer] is [inner] some [/outer] invalid [/inner] markup";
            // so what we want this to become:
            // root
            //      "this"
            //      outer
            //          "is"
            //          inner
            //              "some"
            //      inner
            //          "invalid"
            //      "markup"

            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            tree.children.Should().HaveCount(4);

            // the first and last should be text
            tree.children[0].Should().BeOfType<LineParser.MarkupTextNode>();
            tree.children[3].Should().BeOfType<LineParser.MarkupTextNode>();
            ((LineParser.MarkupTextNode)tree.children[0]).text.Should().Be("This ");
            ((LineParser.MarkupTextNode)tree.children[3]).text.Should().Be(" markup");

            // the outer markup
            tree.children[1].children.Should().HaveCount(2);
            Descendant(tree, 1,1).children.Should().HaveCount(1);
            // the "is" text
            ((LineParser.MarkupTextNode)Descendant(tree, 1,0)).text.Should().Be(" is ");
            // the "some" text inside the inner-inner markup
            Descendant(tree, 1,1,0).Should().BeOfType<LineParser.MarkupTextNode>();
            ((LineParser.MarkupTextNode)Descendant(tree, 1,1,0)).text.Should().Be(" some ");

            // the outer-sibling inner markup
            tree.children[2].children.Should().HaveCount(1);
            Descendant(tree, 2,0).Should().BeOfType<LineParser.MarkupTextNode>();
            ((LineParser.MarkupTextNode)Descendant(tree, 2,0)).text.Should().Be(" invalid ");
        }
        [Fact] public void TestUnsquishedMultipleImbalancedMarkupIsValid()
        {
            var line = "This [a] is [b] some [c] nested [/a] markup [/c] with [/b] invalid structure";
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            // text a b text
            tree.children.Should().HaveCount(4);
            
            // the two outer children should be text
            ((LineParser.MarkupTextNode)tree.children[0]).text.Should().Be("This ");
            ((LineParser.MarkupTextNode)tree.children[3]).text.Should().Be(" invalid structure");

            // the outer a node
            var aNode = tree.children[1];
            aNode.name.Should().Be("a");
            aNode.children.Should().HaveCount(2);
            // outer a's text
            ((LineParser.MarkupTextNode)aNode.children[0]).text.Should().Be(" is ");
            
            var abNode = Descendant(tree, 1,1);
            abNode.name.Should().Be("b");
            abNode.children.Should().HaveCount(2);
            ((LineParser.MarkupTextNode)abNode.children[0]).text.Should().Be(" some ");

            var abcNode = Descendant(tree, 1,1,1);
            abcNode.name.Should().Be("c");
            ((LineParser.MarkupTextNode)abcNode.children[0]).text.Should().Be(" nested ");

            // the outer b node
            var bNode = tree.children[2];
            bNode.name.Should().Be("b");
            bNode.children.Should().HaveCount(2);
            // outer b's text
            ((LineParser.MarkupTextNode)Descendant(tree, 2,1)).text.Should().Be(" with ");

            var bcNode = Descendant(tree, 2,0);
            bcNode.name.Should().Be("c");
            ((LineParser.MarkupTextNode)bcNode.children[0]).text.Should().Be(" markup ");
        }

        internal static LineParser.MarkupTreeNode Node(string name, params LineParser.MarkupTreeNode[] children)
        {
            return new LineParser.MarkupTreeNode
            {
                name = name,
                children = new List<LineParser.MarkupTreeNode>(children)
            };
        }
        internal static LineParser.MarkupTreeNode Text(string value)
        {
            return new LineParser.MarkupTextNode
            {
                text = value,
            };
        }

        public static IEnumerable<object[]> TreeShapes()
        {
            var line = "This [a] is [b] some [c] nested [/a] markup [/c] with [/b] invalid structure.";
            // This [a] is [b] some [c] nested [/c][/b][/a][b][c] markup [/c] with [/b] invalid structure.
            var root = Node(null,
                Text("This "),
                Node("a",
                    Text(" is "),
                    Node("b",
                        Text(" some "),
                        Node("c",
                            Text(" nested ")))),
                Node("b",
                    Node("c",
                        Text(" markup ")),
                    Text(" with ")),
                Text(" invalid structure.")
            );
            yield return new object[] { line, root };

            line = "This [outer] is [inner] some [/outer] invalid [/inner] markup";
            // This [outer] is [inner] some [/inner][/outer][inner] invalid [/inner] markup
            root = Node(null,
                Text("This "),
                Node("outer",
                    Text(" is "),
                    Node("inner", 
                        Text(" some "))),
                Node("inner",
                    Text(" invalid ")),
                Text(" markup")
            );

            yield return new object[] { line, root };
            
            line = "This [outer] is [inner] some [/outer][/inner] markup";
            // This [outer] is [inner] some [/inner][/outer] markup
            // This [outer] is [inner] some [/] markup
            root = Node(null,
                Text("This "),
                Node("outer",
                    Text(" is "),
                    Node("inner",
                        Text(" some ")
                    )
                ),
                Text(" markup")
            );
            yield return new object[] { line, root };

            // [z] this [a] is [b] some [c] markup [d] with [e] both [/c][/e][/d][/a][/z] misclosed tags and double unclosable tags[/b]
            // [z] this [a] is [b] some [c] markup [d] with [e] both [/e][/d][/c][/b][/a][/z][b] misclosed tags and double unclosable tags[/b]
            line = "[z] this [a] is [b] some [c] markup [d] with [e] both [/c][/e][/d][/z][/a] misclosed tags and double unclosable tags[/b]";
            root = Node(null,
                Node("z",
                    Text(" this "),
                    Node("a",
                        Text(" is "),
                        Node("b",
                            Text(" some "),
                            Node("c",
                                Text(" markup "),
                                Node("d",
                                    Text(" with "),
                                    Node("e",
                                        Text(" both ")
                                    )
                                )
                            )
                        )
                    )
                ),
                Node("b",
                    Text(" misclosed tags and double unclosable tags"))
            );
            yield return new object[] { line, root };

            line = "[a]This is [b]some [c]markup[/b] with[/c] closing tag issues inside a valid tag[/a]";
            // [a]This is [b]some [c]markup[/c][/b][c] with[/c] closing tag issues[/a]
            root = Node(null,
                Node("a",
                    Text("This is "),
                    Node("b",
                        Text("some "),
                        Node("c",
                            Text("markup"))),
                    Node("c",
                        Text(" with")),
                    Text(" closing tag issues inside a valid tag"))
            );
            yield return new object[] { line, root };

            line = "[a][b]1 [c][X]2[/b] [d]3[/X][/c] 4[/d] [e]5[/e][/a]";
            root = Node(null,
                Node("a",
                    Node("b",
                        Text("1 "),
                        Node("c",
                            Node("X",
                                Text("2")
                            )
                        )
                    ),
                    Node("c",
                        Node("X",
                            Text(" "),
                            Node("d",
                                Text("3")
                            )
                        )
                    ),
                    Node("d",
                        Text(" 4")
                    ),
                    Text(" "),
                    Node("e",
                        Text("5")
                    )
                )
            );
            yield return new object[] { line, root };
        }
        void CompareWalk(LineParser.MarkupTreeNode left, LineParser.MarkupTreeNode right)
        {
            // we want to know if they are text
            var leftIsText = left is LineParser.MarkupTextNode;
            var rightIsText = right is LineParser.MarkupTextNode;

            // regardless they must be the same
            leftIsText.Should().Be(rightIsText);

            // ok so they are the same now, are they text?
            if (leftIsText)
            {
                var rightText = ((LineParser.MarkupTextNode)right).text;
                ((LineParser.MarkupTextNode)left).text.Should().Be(rightText);
                return;
            }

            // they aren't text so we need to keep walking and comparing
            // first up do they have the same name and number of children
            left.name.Should().Be(right.name);
            left.children.Should().HaveCount(right.children.Count);

            for (int i = 0; i < left.children.Count; i++)
            {
                CompareWalk(left.children[i], right.children[i]);
            }
        }

        [Theory]
        [MemberData(nameof(TreeShapes))]
        void TestUnsquishedTreeConformsToExpectedShape(string line, LineParser.MarkupTreeNode comparison)
        {
            var lineParser = new LineParser();
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            CompareWalk(tree, comparison);
        }

        [Theory]
        [InlineData("this is line without markup","this is line without markup")]
        [InlineData("[a]this is line with basic markup[/a]","this is line with basic markup")]
        [InlineData("[a]this is line with [b]nested basic[/b] markup[/a]","this is line with nested basic markup")]
        [InlineData("this is a[nomarkup] line with [b]nomarkup hiding[/b] markup[/nomarkup] elements","this is a line with [b]nomarkup hiding[/b] markup elements")]
        [InlineData("This is a [bold]line testing basic[/bold] replacement markers", "This is a <b>line testing basic</b> replacement markers")]
        [InlineData("[a]This is [b]some [c]markup[/b] with[/c] closing tag issues inside a valid tag[/a]", "This is some markup with closing tag issues inside a valid tag")]
        void TestUnsquishedMarkupStringsWithRewritersAreValid(string line, string comparison) // this one just tests that the strings aren't fucked
        {
            var lineParser = new LineParser();
            lineParser.RegisterMarkerProcessor("bold", this);
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            var builder = new System.Text.StringBuilder();
            List<MarkupAttribute> attributes = new List<MarkupAttribute>();
            List<LineParser.MarkupDiagnostic> diagnostics = new List<LineParser.MarkupDiagnostic>();
            lineParser.WalkTree(tree, builder, attributes, "en", diagnostics);

            diagnostics.Should().HaveCount(0);

            builder.ToString().Should().Be(comparison);
        }
        
        [Theory]
        [InlineData("this is line without markup","this is line without markup", 0)]
        [InlineData("[a]this is line with basic markup[/a]","this is line with basic markup", 1)]
        [InlineData("[a]this is line with [b]nested basic[/b] markup[/a]","this is line with nested basic markup", 2)]
        [InlineData("this is a[nomarkup] line with [b]nomarkup hiding[/b] markup[/nomarkup] elements","this is a line with [b]nomarkup hiding[/b] markup elements", 1)]
        [InlineData("This is a [bold]line testing basic[/bold] replacement markers", "This is a <b>line testing basic</b> replacement markers", 0)]
        [InlineData("[a]This is [b]some [c]markup[/b] with[/c] closing tag issues inside a valid tag[/a]", "This is some markup with closing tag issues inside a valid tag",3)]
        void TestSquishedMarkupStringsWithRewritersAreValid(string line, string comparison, int attributeCount)
        {
            var lineParser = new LineParser();
            lineParser.RegisterMarkerProcessor("bold", this);
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            var builder = new System.Text.StringBuilder();
            List<MarkupAttribute> attributes = new List<MarkupAttribute>();
            List<LineParser.MarkupDiagnostic> diagnostics = new List<LineParser.MarkupDiagnostic>();
            lineParser.WalkTree(tree, builder, attributes, "en", diagnostics);
            
            diagnostics.Should().HaveCount(0);

            builder.ToString().Should().Be(comparison);
            lineParser.SquishSplitAttributes(attributes);

            attributes.Should().HaveCount(attributeCount);
        }

        List<LineParser.MarkupDiagnostic> IAttributeMarkerProcessor.ProcessReplacementMarker(MarkupAttribute marker, StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode)
        {
            var diagnostics = new List<LineParser.MarkupDiagnostic>();
            switch (marker.Name)
            {
                case "bold":
                {
                    // for now I am just gonna make it so that it replaces [bold] some text [/bold] with 
                    // <b> some text </b>
                    childBuilder.Insert(0, "<b>");
                    childBuilder.Append("</b>");
                    
                    return diagnostics;
                }
                case "localise":
                {
                    // this is bad Tim...
                    if (localeCode == "en")
                    {
                        childBuilder.Append("cat");
                    }
                    else
                    {
                        childBuilder.Append("chat");
                    }
                    return diagnostics;
                }
                default:
                {
                    childBuilder.Append("Unrecognised markup name: ");
                    childBuilder.Append(marker.Name);
                    return diagnostics;
                }
            }
        }
        [Fact]
        void TestLocalisedStringReplacement()
        {
            var line = "This is my pet [localise = cat /], [b]Pumpkin![/b]";
            var en_comparison = "This is my pet cat, Pumpkin!";
            var fr_comparison = "This is my pet chat, Pumpkin!";

            var lineParser = new LineParser();
            lineParser.RegisterMarkerProcessor("localise", this);
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            var builder = new System.Text.StringBuilder();
            List<MarkupAttribute> attributes = new List<MarkupAttribute>();
            List<LineParser.MarkupDiagnostic> diagnostics = new List<LineParser.MarkupDiagnostic>();
            lineParser.WalkTree(tree, builder, attributes, "en", diagnostics);

            diagnostics.Should().HaveCount(0);

            builder.ToString().Should().Be(en_comparison);
            lineParser.SquishSplitAttributes(attributes);

            attributes.Should().HaveCount(1);

            builder = new System.Text.StringBuilder();
            attributes = new List<MarkupAttribute>();
            diagnostics = new List<LineParser.MarkupDiagnostic>();
            lineParser.WalkTree(tree, builder, attributes, "fr", diagnostics);

            diagnostics.Should().HaveCount(0);

            builder.ToString().Should().Be(fr_comparison);
            lineParser.SquishSplitAttributes(attributes);

            attributes.Should().HaveCount(1);
        }

        [Theory]
        [MemberData(nameof(RangeComparisons))]
        void TestSquishedRangesAreValid(string line, string comparison, int expectedAttributes, Dictionary<string, (int, int)> ranges)
        {
            var lineParser = new LineParser();
            lineParser.RegisterMarkerProcessor("bold", this);
            var tokens = lineParser.LexMarkup(line);
            var result = lineParser.BuildMarkupTreeFromTokens(tokens, line);
            var tree = result.tree;
            var errors = result.diagnostics;
            errors.Should().BeEmpty();

            var builder = new System.Text.StringBuilder();
            List<MarkupAttribute> attributes = new List<MarkupAttribute>();
            List<LineParser.MarkupDiagnostic> diagnostics = new List<LineParser.MarkupDiagnostic>();
            lineParser.WalkTree(tree, builder, attributes, "en", diagnostics);

            diagnostics.Should().HaveCount(0);

            builder.ToString().Should().Be(comparison);
            lineParser.SquishSplitAttributes(attributes);

            attributes.Should().HaveCount(expectedAttributes);
            ranges.Should().HaveCount(expectedAttributes);

            foreach (var attribute in attributes)
            {
                var comparisonRange = ranges[attribute.Name];
                attribute.Length.Should().Be(comparisonRange.Item2);
                attribute.Position.Should().Be(comparisonRange.Item1);
            }
        }
        public static IEnumerable<object[]> RangeComparisons()
        {
            var line = "[a]this is line with basic markup[/a]";
            var comparison = "this is line with basic markup";
            var attributeCount = 1;
            var ranges = new Dictionary<string, (int, int)>
            {
                {"a", (0, 30)}
            };
            yield return new object[] { line, comparison, attributeCount, ranges };

            line = "[a]this is line with [b]nested basic[/b] markup[/a]";
            comparison = "this is line with nested basic markup";
            attributeCount = 2;
            ranges = new Dictionary<string, (int, int)>
            {
                {"a", (0, 37)},
                {"b", (18, 12)},
            };
            yield return new object[] { line, comparison, attributeCount, ranges };

            line = "[a]This is [b]some [c]markup[/b] with[/c] closing tag issues inside a valid tag[/a]";
            comparison = "This is some markup with closing tag issues inside a valid tag";
            attributeCount = 3;
            ranges = new Dictionary<string, (int, int)>
            {
                {"a", (0, 62)},
                {"b", (8, 11)},
                {"c", (13, 11)},
            };
            yield return new object[] { line, comparison, attributeCount, ranges };

            line = "this[z] here[a] is[b] some[c] markup[d] with[e] both[/c][/e][/d][/a][/z] misclosed tags and double unclosable tags[/b]";
            comparison = "this here is some markup with both misclosed tags and double unclosable tags";
            attributeCount = 6;
            ranges = new Dictionary<string, (int, int)>
            {
                {"z", (4, 30)},
                {"a", (9, 25)},
                {"c", (17, 17)},
                {"d", (24, 10)},
                {"e", (29, 5)},
                {"b", (12, 64)}
            };
            yield return new object[] { line, comparison, attributeCount, ranges };

            line = "[a][b]1 [c][X]2[/b] [d]3[/X][/c] 4[/d] [e]5[/e][/a]";
            comparison = "1 2 3 4 5";
            ranges = new Dictionary<string, (int, int)>
            {
                {"a", (0,9)},
                {"b", (0,3)},
                {"c", (2,3)},
                {"X", (2,3)},
                {"d", (4,3)},
                {"e", (8,1)}
            };
            /*
            ROOT
                a
                    b
                        "1 "
                        c
                            X
                                "2"
                    c
                        X
                            " "
                            d
                                "3"
                    d
                        " 4"
                    " "
                    e
                        "5"
            */
            yield return new object[] { line, comparison, attributeCount, ranges };
        }

        [Fact]
        public void TestOverlappingAttributes()
        {
            var line = "[a][b][c]X[/b][/a]X[/c]";

            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

            markup.Attributes.Count.Should().Be(3);
            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[1].Name.Should().Be("b");
            markup.Attributes[2].Name.Should().Be("c");
        }

        [Fact]
        public void TestTextExtraction()
        {
            var line = "A [b]B [c]C[/c][/b]";

            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

            markup.TextForAttribute(markup.Attributes[0]).Should().Be("B C");
            markup.TextForAttribute(markup.Attributes[1]).Should().Be("C");
        }

        [Fact]
        public void TestAttributeRemoval()
        {
            // A test string with the following attributes:
            // a: Covers the entire string
            // b: Starts outside X, ends inside
            // c: Same start and end point as X
            // d: Starts inside X, ends outside
            // e: Starts and ends outside X
            var line = "[a][b]A [c][X]x[/b] [d]x[/X][/c] B[/d] [e]C[/e][/a]";

            var lineParser = new LineParser();
            var originalMarkup = lineParser.ParseString(line, "en");

            // find and Remove the "X" attribute
            originalMarkup.TryGetAttributeWithName("X", out var xAttribute).Should().Be(true);
            var trimmedMarkup = originalMarkup.DeleteRange(xAttribute);
            
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

            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

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
        public void TestMultibyteCharacterParsing(string input)
        {
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(input, "en");

            // All versions of this string should have the same position
            // and length of the attribute, despite the presence of
            // multibyte characters
            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(1);
        }

        [Theory]
        [InlineData("á: [á]S[/á]")]
        [InlineData("á: [a]á[/a]")]
        [InlineData("á: [a]S[/a]")]
        [InlineData("S: [á]S[/á]")]
        [InlineData("S: [a]á[/a]")]
        [InlineData("S: [a]S[/a]")]
        public void TestMultibyteCharacterParsingWithImplicitCharacterAttributes(string input)
        {
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(input, "en");

            // All versions of this string should have the same position
            // and length of the attribute, despite the presence of
            // multibyte characters
            markup.Attributes.Should().HaveCount(2);
            markup.Attributes[0].Position.Should().Be(0);
            markup.Attributes[0].Length.Should().Be(3);
            
            markup.Attributes[1].Position.Should().Be(3);
            markup.Attributes[1].Length.Should().Be(1);
        }

        [Theory]
        [InlineData("[a][/a][/b]")]
        [InlineData("[/b]")]
        [InlineData("[a][/][/b]")]
        public void TestUnexpectedCloseMarkerErrors(string input)
        {
            var lineParser = new LineParser();
            var results = lineParser.ParseStringWithDiagnostics(input, "en");

            results.diagnostics.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public void TestMarkupShortcutPropertyParsing()
        {
            var line = "[a=1]s[/a]";
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

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
        public void TestMarkupMultiplePropertyParsing()
        {
            var line = "[a p1=1 p2=2]s[/a]";
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

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
        [InlineData("[a p={$someValue}]s[/a]", MarkupValueType.String, "$someValue")]
        public void TestMarkupPropertyParsing(string input, MarkupValueType expectedType, string expectedValueAsString)
        {
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(input, "en");

            var attribute = markup.Attributes[0];
            var propertyValue= attribute.Properties["p"];

            propertyValue.Type.Should().Be(expectedType);
            propertyValue.ToString().Should().Be(expectedValueAsString);
        }

        [Theory]
        [InlineData("A [b]B [c]C[/c][/b] D")] // attributes can be closed
        [InlineData("A [b]B [c]C[/b][/c] D")] // attributes can be closed out of order
        [InlineData("A [b]B [c]C[/] D")] // "[/]" closes all open attributes
        public void TestMultipleAttributes(string input)
        {
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(input, "en");

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
        public void TestSelfClosingAttributes()
        {
            var line = "A [a/] B";
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

            markup.Text.Should().Be("A B");

            markup.Attributes.Should().ContainSingle();

            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[0].Properties.Count.Should().Be(1); // one because of the implicit trimwhitespace attribute on self-closing markup
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(0);
        }

        [Theory]
        [InlineData("A [a/] B", "A B")]
        [InlineData("A [a trimwhitespace=true/] B", "A B")]
        [InlineData("A [a trimwhitespace=false/] B", "A  B")]
        [InlineData("A [nomarkup trimwhitespace=false/] B", "A  B")]
        [InlineData("A [nomarkup trimwhitespace=true/] B", "A B")]
        public void TestAttributesMayTrimTrailingWhitespace(string input, string expectedText)
        {
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(input, "en");

            markup.Text.Should().Be(expectedText);
        }

        [Theory]
        // character attribute can be implicit
        [InlineData("Mae: Wow!")] 
        // character attribute can also be explicit
        [InlineData("[character name=\"Mae\"]Mae: [/character]Wow!")] 
        public void TestImplicitCharacterAttributeParsing(string input)
        {
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(input, "en");

            markup.Text.Should().Be("Mae: Wow!");
            markup.Attributes.Should().ContainSingle();

            markup.Attributes[0].Name.Should().Be("character");
            markup.Attributes[0].Position.Should().Be(0);
            markup.Attributes[0].Length.Should().Be(5);

            markup.Attributes[0].Properties.Count.Should().Be(1);
            markup.Attributes[0].Properties["name"].StringValue.Should().Be("Mae");
        }

        [Fact]
        public void TestNoMarkupModeParsing()
        {
            var line = "S [a]S[/a] [nomarkup][a]S;][/a][/nomarkup]";
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

            markup.Text.Should().Be("S S [a]S;][/a]");

            // a and nomarkup
            markup.Attributes.Should().HaveCount(2);

            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[0].Position.Should().Be(2);
            markup.Attributes[0].Length.Should().Be(1);

            markup.Attributes[1].Name.Should().Be("nomarkup");
            markup.Attributes[1].Position.Should().Be(4);
            markup.Attributes[1].Length.Should().Be(10);
        }

        [Fact]
        public void TestMarkupEscaping()
        {
            var line = @"[a]hello \[b\]hello\[/b\][/a]";
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

            markup.Text.Should().Be("hello [b]hello[/b]");
            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Name.Should().Be("a");
            markup.Attributes[0].Position.Should().Be(0);
            markup.Attributes[0].Length.Should().Be(18);
        }

        [Fact]
        public void TestNumericSelection()
        {
            var line = @"[select value=1 1=one 2=two 3=three /]";
            var lineParser = new LineParser();
            var markup = lineParser.ParseString(line, "en");

            markup.Attributes.Should().ContainSingle();
            markup.Attributes[0].Name.Should().Be("select");
            markup.Attributes[0].Properties.Should().HaveCount(5);
            markup.Attributes[0].Properties["value"].IntegerValue.Should().Be(1);
            markup.Attributes[0].Properties["1"].StringValue.Should().Be("one");
            markup.Attributes[0].Properties["2"].StringValue.Should().Be("two");
            markup.Attributes[0].Properties["3"].StringValue.Should().Be("three");
            markup.Attributes[0].Properties["trimwhitespace"].BoolValue.Should().Be(true);

            // now that we know it is correctly determining all the elements run it again with the rewriters enabled
            BuiltInMarkupReplacer select = new BuiltInMarkupReplacer();
            lineParser.RegisterMarkerProcessor("select", select);
            markup = lineParser.ParseString(line, "en");

            markup.Text.Should().Be("one");
        }

        [Theory]
        [InlineData(1,"en", "a single cat")]
        [InlineData(2,"en", "2 cats")]
        [InlineData(3,"en", "3 cats")]
        [InlineData(1,"en-AU", "a single cat")]
        [InlineData(2,"en-AU", "2 cats")]
        [InlineData(3,"en-AU", "3 cats")]
        public void TestNumberPluralisation(int Value, string Locale, string Expected)
        {
            var line = "[plural value=" + Value + " one=\"a single cat\" other=\"% cats\"/]";
            var lineParser = new LineParser();
            BuiltInMarkupReplacer rewriter = new BuiltInMarkupReplacer();
            lineParser.RegisterMarkerProcessor("plural", rewriter);
            var markup = lineParser.ParseString(line, Locale);
            markup.Text.Should().Be(Expected, $"{Value} in locale {Locale} should have the correct plural case");
        }
    }
}
