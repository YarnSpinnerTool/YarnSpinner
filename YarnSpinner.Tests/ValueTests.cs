using Xunit;
using System;
using Yarn;

namespace YarnSpinner.Tests
{
    public class ValueTests : TestBase
    {
        [Theory]
        [InlineData((sbyte)1, Yarn.Value.Type.Number)]
        [InlineData((byte)1, Yarn.Value.Type.Number)]
        [InlineData((short)1, Yarn.Value.Type.Number)]
        [InlineData((ushort)1, Yarn.Value.Type.Number)]
        [InlineData((int)1, Yarn.Value.Type.Number)]
        [InlineData((uint)1, Yarn.Value.Type.Number)]
        [InlineData((long)1, Yarn.Value.Type.Number)]
        [InlineData((ulong)1, Yarn.Value.Type.Number)]
        [InlineData((float)1, Yarn.Value.Type.Number)]
        [InlineData((double)1, Yarn.Value.Type.Number)]
        [InlineData((float)1, Yarn.Value.Type.Number)]
        [InlineData("testing strings", Yarn.Value.Type.String)]
        [InlineData(true, Yarn.Value.Type.Bool)]
        [InlineData((object)null, Yarn.Value.Type.Null)]
        public void TestCreateValue(object val, Yarn.Value.Type expectedType = Yarn.Value.Type.Null)
        {
            var yarnValue = new Value(val);
            Assert.Equal(expectedType, yarnValue.type);
        }
    }
}