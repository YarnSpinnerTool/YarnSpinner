namespace Yarn.Compiler
{
    using System;
    using System.Globalization;

    /// <summary>
    /// An exception representing something going wrong during parsing.
    /// </summary>
    [Serializable]
    public sealed class ParseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParseException"/>
        /// class.
        /// </summary>
        /// <param name="message">The message associated with this
        /// exception.</param>
        internal ParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParseException"/>
        /// class.
        /// </summary>
        /// <param name="context">The <see
        /// cref="Antlr4.Runtime.ParserRuleContext"/> for the error that
        /// resulted in this exception being thrown.</param>
        /// <param name="message">The message associated with this
        /// exception. </param>
        internal ParseException(Antlr4.Runtime.ParserRuleContext context, string message)
            : this(CreateErrorMessageForContext(context, message))
        {
            this.LineNumber = context.Start.Line;
        }

        /// <summary>
        /// Gets or sets the line number at which this parse exception occurred.
        /// </summary>
        internal int LineNumber { get; set; } = 0;

        private static string CreateErrorMessageForContext(Antlr4.Runtime.ParserRuleContext context, string message)
        {
            int line = context.Start.Line;

            // getting the text that has the issue inside
            int start = context.Start.StartIndex;
            int end = context.Stop.StopIndex;
            string body = context.Start.InputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, end));

            string theMessage = string.Format(CultureInfo.CurrentCulture, "Error on line {0}\n{1}\n{2}", line, body, message);

            return theMessage;
        }
    }
}
