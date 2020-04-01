namespace Yarn {

    /// <summary>
    /// An exception that is thrown by <see cref="Dialogue"/> when there is an error in executing a <see cref="Program"/>.
    /// </summary>
    [System.Serializable]
    public class DialogueException : System.Exception
    {
        internal DialogueException() { }
        internal DialogueException(string message) : base(message) { }
        internal DialogueException(string message, System.Exception inner) : base(message, inner) { }
        internal DialogueException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

