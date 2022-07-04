using System;
using System.Collections.Generic;

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
    }
}
