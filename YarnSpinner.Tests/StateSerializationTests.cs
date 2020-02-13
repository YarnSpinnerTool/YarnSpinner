using Xunit;
using System;
using System.IO;
using System.Text;
using System.Diagnostics;

using Google.Protobuf;

using Yarn;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{
    public class StateSerializationTests : TestBase
    {
        [Fact]
        public void TestSettingState()
        {
            var path = Path.Combine(TestDataPath, "Example.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);
            State state = new State();

            // State cant be set if its CurrentNodeName is empty
            Assert.Empty(state.CurrentNodeName);
            Assert.False(dialogue.TrySetState(state));

            // Set Node that doesnt exist in program.
            state.CurrentNodeName = "Unknown";

            // State cant be set if program is not set.
            Assert.Null(dialogue.Program);
            Assert.False(dialogue.TrySetState(state));

            // Set program
            dialogue.SetProgram(program);

            // State cant be set if Node doesnt exist in program
            Assert.False(dialogue.TrySetState(state));

            // Set existing Node
            state.CurrentNodeName = "Start";

            // At this point checks should have passed and State is valid to be set
            Assert.True(dialogue.TrySetState(state));
        }

        [Fact]
        public void TestDeserializingBinaryState()
        {
            var path = Path.Combine(TestDataPath, "Example.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);
            State state = new State();

            // Program needs to be set in VM
            dialogue.SetProgram(program);
            
            // State requires just a valid Node name
            state.CurrentNodeName = "Start";

            // Program is set, it has the node Start and state is going to run Start
            // All should be well
            Assert.True(dialogue.TrySetState(state));

            // Serialize the state
            BinarySerializedState binary = new BinarySerializedState(state);

            // Deserialize the state back
            State deserialized = binary.Deserialize();

            // Try setting the state
            Assert.True(dialogue.TrySetState(deserialized));
        }

        [Fact]
        public void TestDeserializingJsonState()
        {
            var path = Path.Combine(TestDataPath, "Example.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);
            State state = new State();

            // Program needs to be set in VM
            dialogue.SetProgram(program);

            // State requires just a valid Node name
            state.CurrentNodeName = "Start";

            // Program is set, it has the node Start and state is going to run Start
            // All should be well
            Assert.True(dialogue.TrySetState(state));

            // Serialize the state
            JsonSerializedState json = new JsonSerializedState(state);

            // Deserialize the state back
            State deserialized = json.Deserialize();

            // Try setting the state
            Assert.True(dialogue.TrySetState(deserialized));
        }

        [Fact]
        public void TestGettingStateClone()
        {
            var path = Path.Combine(TestDataPath, "Example.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);
            dialogue.SetProgram(program);

            State state;

            // Node must be set for getting a valid state.
            // Check if this behaviour works
            state = dialogue.GetStateClone();
            Assert.Null(state);

            // Setting a node should properly get data.
            dialogue.SetNode("Start");
            state = dialogue.GetStateClone();
            Assert.NotNull(state);
        }

        [Fact]
        public void TestGettingBinarySerializedState()
        {
            var path = Path.Combine(TestDataPath, "Example.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);
            dialogue.SetProgram(program);

            BinarySerializedState binary;

            // Node must be set for serializer to return the state.
            // Check if this behaviour works
            binary = dialogue.GetStateBinarySerialized();
            Assert.Null(binary);

            // Setting a node should properly get data.
            dialogue.SetNode("Start");
            binary = dialogue.GetStateBinarySerialized();
            Assert.NotNull(binary);

            // Test ByteString format.
            Assert.IsType<ByteString>(binary.GetData());

            // Test string format.
            Assert.IsType<string>(binary.ToString(Encoding.UTF8));

            // Test Base64 format.
            bool isValidBase64 = false;
            try {
                Convert.FromBase64String(binary.ToBase64());
                isValidBase64 = true;
            } catch {}

            Assert.True(isValidBase64);

            // Test byte[] format.
            Assert.IsType<byte[]>(binary.ToByteArray());
        }

        [Fact]
        public void TestGettingJsonSerializedState()
        {
            var path = Path.Combine(TestDataPath, "Example.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);
            dialogue.SetProgram(program);

            JsonSerializedState json;

            // Node must be set for serializer to return the state.
            // Check if this behaviour works
            json = dialogue.GetStateJsonSerialized();
            Assert.Null(json);

            // Setting a node should properly get data.
            dialogue.SetNode("Start");
            json = dialogue.GetStateJsonSerialized();
            Assert.NotNull(json);

            // Test string format.
            Assert.IsType<string>(json.GetData());
        }
    }
}

