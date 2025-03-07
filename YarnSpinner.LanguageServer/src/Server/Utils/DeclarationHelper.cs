namespace YarnLanguageServer;

public static class DeclarationHelper
{
    /// <summary>
    /// Given a declaration, produces the appropriate Yarn keywords for the
    /// type, and a syntactically-correct version of the declaration's initial
    /// value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is useful for producing a <c>&lt;&lt;declare&gt;&gt;</c>
    /// statement's syntax.
    /// </para>
    /// <para>
    /// If the type of the declaration is neither string, number nor boolean,
    /// the value of <paramref name="type"/> will be "undefined", and the value
    /// of <paramref name="defaultValue"/> will be "(?)".
    /// </para>
    /// </remarks>
    /// <param name="existingDeclaration">The variable declaration.</param>
    /// <param name="type">On return, contains the Yarn keyword for the
    /// declaration's type.</param>
    /// <param name="defaultValue">On return, contains a syntactically-correct
    /// version of the declaration's initial value.</param>
    public static void GetDeclarationInfo(Yarn.Compiler.Declaration existingDeclaration, out string type, out string defaultValue)
    {
        if (existingDeclaration.Type == Yarn.Types.String)
        {
            type = "string";
            defaultValue = $"\"{existingDeclaration.DefaultValue ?? string.Empty}\"";
        }
        else if (existingDeclaration.Type == Yarn.Types.Number)
        {
            type = "number";
            defaultValue = $"{existingDeclaration.DefaultValue ?? 0}";
        }
        else if (existingDeclaration.Type == Yarn.Types.Boolean)
        {
            type = "bool";
            defaultValue = $"{existingDeclaration.DefaultValue?.ToString()?.ToLowerInvariant() ?? "false"}";
        }
        else
        {
            type = existingDeclaration.Type?.Name ?? "undefined";
            defaultValue = existingDeclaration.DefaultValue?.ToString() ?? "(?)";
        }
    }
}
