using System;

// This file is not compiled, because TestData is excluded from the project.

public class ExampleCommands {
    // This is an example of a static command with no parameters, and doesn't
    // have a documentation comment. (The method is below this field to prevent
    // the parser from associating this comment with the method.)
    int sep;
    [YarnCommand("static_command_no_docs")]
    public static void StaticCommandNoDocs() {
    }

    /// <summary>
    /// This is an example of a static command with no parameters.
    /// </summary>
    [YarnCommand("static_command_no_params")]
    public static void StaticCommandNoParams() {
    }

    /// <summary>
    /// This is an example of a static command with parameters.
    /// </summary>
    /// <param name="stringParam">The string parameter.</param>
    /// <param name="intParam">The integer parameter.</param>
    [YarnCommand("static_command_with_params")]
    public static void StaticCommandWithParams(string stringParam, int intParam) {
    }

    /// <summary>
    /// This is an example of an instance command with no parameters.
    /// </summary>
    [YarnCommand("instance_command_no_params")]
    public void InstanceCommandNoParams() {
    }

    /// <summary>
    /// This is an example of an instance command with parameters.
    /// </summary>
    /// <param name="stringParam">The string parameter.</param>
    /// <param name="intParam">The integer parameter.</param>
    [YarnCommand("instance_command_with_params")]
    public void InstanceCommandWithParams(string stringParam, int intParam) {
    }

    [YarnFunction("function_with_params")]
    public static int FunctionWithParams(int one, string two) {
        return -1;
    }

    /// <summary>
    /// This command has <c>nested XML</c> nodes.
    /// </summary>
    [YarnCommand("command_with_complex_documentation")]
    public static void CommandWithComplexDocs() {

    }
}
