using System;

namespace Yarn
{
    internal class ErrorType : TypeBase
    {
        public ErrorType() : base(null)
        {
        }

        public override string Name => "<ERROR>";

        public override IType Parent => null;

        public override string Description => "(type error)";

        internal override System.IConvertible DefaultValue => throw new InvalidOperationException();
    }
}
