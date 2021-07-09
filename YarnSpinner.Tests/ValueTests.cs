using Xunit;
using System;
using Yarn;

namespace YarnSpinner.Tests
{
    public class ValueTests : TestBase
    {
        [Theory]
        [InlineData((sbyte)1, Yarn.Type.Number)]
        [InlineData((byte)1, Yarn.Type.Number)]
        [InlineData((short)1, Yarn.Type.Number)]
        [InlineData((ushort)1, Yarn.Type.Number)]
        [InlineData((int)1, Yarn.Type.Number)]
        [InlineData((uint)1, Yarn.Type.Number)]
        [InlineData((long)1, Yarn.Type.Number)]
        [InlineData((ulong)1, Yarn.Type.Number)]
        [InlineData((float)1, Yarn.Type.Number)]
        [InlineData((double)1, Yarn.Type.Number)]
        [InlineData((float)1, Yarn.Type.Number)]
        [InlineData("testing strings", Yarn.Type.String)]
        [InlineData(true, Yarn.Type.Bool)]
        public void TestCreateValue(object val, Yarn.Type expectedType)
        {
            var yarnValue = new Value(val);
            Assert.Equal(expectedType, yarnValue.type);
        }
    }
}
