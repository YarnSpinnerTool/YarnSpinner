namespace YarnLanguageServer;

public static class Commands
{
    /// <summary>
    /// The command to list all nodes in a file. 
    /// </summary>
    /// <remarks>
    /// <para>Parameters:</para>
    /// <list type="bullet">
    /// <item>URI to Yarn script</item>
    /// </list>
    /// <para>Returns:</para>
    /// <list type="bullet">
    /// <item>List of node names.</item>
    /// </list>
    /// </remarks>
    public const string ListNodes = "yarnSpinner.listNodes";
}