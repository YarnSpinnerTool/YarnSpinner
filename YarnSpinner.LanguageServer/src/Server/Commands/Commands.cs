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

    /// <summary>
    /// The command to compile the current Yarn project and get back the byte
    /// array of the compiled program.
    /// </summary>
    public const string CompileCurrentProject = "yarnspinner.compile";

    /// <summary>
    /// The command to compile a Yarn project and get a spreadsheet.
    /// </summary>
    public const string ExtractSpreadsheet = "yarnspinner.extract-spreadsheet";

    /// <summary>
    /// The command to generate a graph of the Yarn Project.
    /// Will be presented as a directed graph of nodes and jumps.
    /// </summary>
    public const string CreateDialogueGraph = "yarnspinner.create-graph";

    /// <summary>
    /// The command to show references to a named Yarn node.
    /// </summary>
    public const string ShowReferences = "yarn.showReferences";

    /// <summary>
    /// The command to show a specific Yarn node in a graph view.
    /// </summary>
    public const string ShowNodeInGraphView = "yarn.showNodeInGraphView";
}
