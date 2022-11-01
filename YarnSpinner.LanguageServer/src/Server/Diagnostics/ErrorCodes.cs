namespace YarnLanguageServer.Diagnostics
{
    /// <summary>
    /// Diagnostic Codes to make sure we are pairing errors with their handlers.
    /// </summary>
    public enum YarnDiagnosticCode
    {
        /// <summary>
        /// Error: Variable declared more than once
        /// </summary>
        YRNDupVarDec,

        /// <summary>
        /// Error: Variable has no declaration
        /// </summary>
        YRNMsngVarDec,

        /// <summary>
        /// Warning: Command or Function that has no associated definition
        /// </summary>
        YRNMsngCmdDef,

        /// <summary>
        /// Error: Command or Function that has an incorrect number of parameters
        /// </summary>
        YRNCmdParamCnt,
    }
}
