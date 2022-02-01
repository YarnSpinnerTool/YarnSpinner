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
    /// The command to create a new node in the file.
    /// </summary>
    public const string AddNode = "yarnspinner.create-node";

    /// <summary>
    /// The command to remove a node with a given title from the file.
    /// </summary>
    public const string RemoveNode = "yarnspinner.remove-node";

    /// <summary>
    /// The command to create or update a header for a node in a file.
    /// </summary>
    public const string UpdateNodeHeader = "yarnspinner.update-node-header";

    /// <summary>
    /// A notification that the nodes in a file have changed.
    /// </summary>
    /// <seealso cref="NodesChangedParams"/>
    public const string DidChangeNodesNotification = "textDocument/yarnSpinner/didChangeNodes";
    
}