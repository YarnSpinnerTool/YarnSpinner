namespace Yarn
{
    internal class ErrorType : TypeBase
    {
        public override string Name => "<ERROR>";

        public override IType Parent => null;

        public override string Description => "(type error)";
    }
}
