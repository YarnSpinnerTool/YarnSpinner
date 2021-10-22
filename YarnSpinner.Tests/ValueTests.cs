using Xunit;
using System;
using Yarn;

namespace YarnSpinner.Tests
{
    public class ValueTests : TestBase
    {
        // [Theory]
        // [InlineData((sbyte)1, BuiltinTypes.Number)]
        // [InlineData((byte)1, BuiltinTypes.Number)]
        // [InlineData((short)1, BuiltinTypes.Number)]
        // [InlineData((ushort)1, BuiltinTypes.Number)]
        // [InlineData((int)1, BuiltinTypes.Number)]
        // [InlineData((uint)1, BuiltinTypes.Number)]
        // [InlineData((long)1, BuiltinTypes.Number)]
        // [InlineData((ulong)1, BuiltinTypes.Number)]
        // [InlineData((float)1, BuiltinTypes.Number)]
        // [InlineData((double)1, BuiltinTypes.Number)]
        // [InlineData((float)1, BuiltinTypes.Number)]
        // [InlineData("testing strings", BuiltinTypes.String)]
        // [InlineData(true, BuiltinTypes.Boolean)]
        public void TestCreateValue(object val, Yarn.IType expectedType)
        {
            Assert.True(false);
            // var yarnValue = new Value(val);
            // Assert.Equal(expectedType, yarnValue.type);
        }
    }
}
