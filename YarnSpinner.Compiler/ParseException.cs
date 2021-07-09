namespace Yarn.Compiler
{
    using System;
    using System.Globalization;

    /// <summary>
    /// An exception representing something going wrong during compilation.
    /// </summary>
    [Serializable]
    public abstract class CompilerException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompilerException"/>
        /// class.
        /// </summary>
        /// <param name="message">The message associated with this
        /// exception.</param>
        internal CompilerException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompilerException"/>
        /// class.
        /// </summary>
        /// <param name="context">The <see
        /// cref="Antlr4.Runtime.ParserRuleContext"/> for the error that
        /// resulted in this exception being thrown.</param>
        /// <param name="message">The message associated with this
        /// exception. </param>
        internal CompilerException(Antlr4.Runtime.ParserRuleContext context, string message, string fileName = "(unknown)")
            : this(CreateErrorMessageForContext(context, message, fileName))
        {
            this.FileName = fileName;
            this.LineNumber = context?.Start.Line ?? 0;
            this.Context = context;
            this.InternalMessage = message;
        }

        public string FileName { get; private set; } = "(unknown)";

        internal Antlr4.Runtime.ParserRuleContext Context { get; private set; }
        internal string InternalMessage { get; private set; }

        /// <summary>
        /// Gets or sets the line number at which this compiler exception occurred.
        /// </summary>
        internal int LineNumber { get; private set; } = 0;

        private static string CreateErrorMessageForContext(Antlr4.Runtime.ParserRuleContext context, string message, string fileName = "(unknown)")
        {
            if (context == null)
            {
                return message;
            }

            int lineNumber = context.Start.Line;

            // getting the text that has the issue inside
            var interval = new Antlr4.Runtime.Misc.Interval(0, context.Start.InputStream.Size);
            string[] lines = context.Start.InputStream.GetText(interval).Split('\n');
            string line = lines[context.Start.Line - 1];

            string theMessage = string.Format(CultureInfo.CurrentCulture, "Error in {3} line {0}\n{1}\n{2}", lineNumber, line, message, fileName);

            return theMessage;
        }
    }

    /// <summary>
    /// An exception representing something going wrong during parsing.
    /// </summary>
    [Serializable]
    public sealed class ParseException : CompilerException
    {
        internal ParseException(string message) : base(message) { }

        internal ParseException(Antlr4.Runtime.ParserRuleContext context, string message, string fileName = "(unknown)") : base(context, message, fileName) { }
    }

    /// <summary>
    /// An exception representing something going wrong during type checking.
    /// </summary>
    [Serializable]
    public sealed class TypeException : CompilerException
    {
        internal TypeException(string message) : base(message) { }

        internal TypeException(Antlr4.Runtime.ParserRuleContext context, string message, string fileName) : base(context, message, fileName) { }
    }
}
