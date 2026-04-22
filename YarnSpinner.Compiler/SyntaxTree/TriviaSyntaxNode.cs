#nullable enable

namespace Yarn.Compiler
{
    public class TriviaSyntaxNode : SyntaxNode
    {
        readonly string text = string.Empty;
        public override string Text => text;

        public TriviaSyntaxNode(SyntaxNode? parent, string text) : base(parent)
        {
            this.text = text;
        }

        public override int Length => text.Length;
    }
}
