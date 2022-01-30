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
    public const string ListNodes = "yarnspinner.list-nodes";

    /// <summary>
    /// A notification that the nodes in a file have changed.
    /// </summary>
    /// <seealso cref="NodesChangedParams"/>
    public const string DidChangeNodesNotification = "textDocument/yarnSpinner/didChangeNodes";
    
}