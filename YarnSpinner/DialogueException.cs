namespace Yarn {
    [System.Serializable]
    public class DialogueException : System.Exception
    {
        public DialogueException() { }
        public DialogueException(string message) : base(message) { }
        public DialogueException(string message, System.Exception inner) : base(message, inner) { }
        protected DialogueException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

