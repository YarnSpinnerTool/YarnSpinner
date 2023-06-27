// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    /// <summary>
    /// An exception that is thrown by <see cref="Dialogue"/> when there is an error in executing a <see cref="Program"/>.
    /// </summary>
    [System.Serializable]
    public class DialogueException : System.Exception
    {
        internal DialogueException() { }
        internal DialogueException(string message) : base(message) { }
        internal DialogueException(string message, System.Exception inner) : base(message, inner) { }
        protected DialogueException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

