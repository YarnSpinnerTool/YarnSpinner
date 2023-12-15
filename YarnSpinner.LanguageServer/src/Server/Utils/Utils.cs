using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer
{
    internal static class Utils
    {
        public static readonly string YarnSelectorPattern = "**/*.yarn";
        public static readonly string YslsJsonSelectorPattern = "**/*.ysls.json";
        public static readonly string CSharpSelectorPattern = "**/*.cs";
        public static readonly string YarnProjectSelectorPattern = "**/*.yarnproject";

        /// <summary>
        /// Selector for any .yarn file in the workspace.
        /// </summary>
        public static readonly DocumentSelector YarnDocumentSelector = new DocumentSelector(
            new DocumentFilter
            {
                Pattern = YarnSelectorPattern,
            });

        public static readonly string YarnLanguageID = "yarn";
        public static readonly string CSharpLanguageID = "csharp";

        /// <summary>
        /// Editor command to trigger parameter hinting (useful when using snippets).
        /// </summary>
        public static readonly Command TriggerParameterHintsCommand = new Command
        {
            Name = "editor.action.triggerParameterHints",
            Title = "editor.action.triggerParameterHints",
        };

        [return: NotNullIfNotNull(nameof(@default))]
        [return: NotNullIfNotNull(nameof(str))]
        public static string? OrDefault(this string? str,  string? @default = default)
        {
            return string.IsNullOrEmpty(str) ? @default : str;
        }

        [return: NotNullIfNotNull(nameof(@default))]
        [return: NotNullIfNotNull(nameof(str))]
        public static object? OrDefault(this string? str, object? @default)
        {
            return string.IsNullOrEmpty(str) ? @default : str;
        }

        public static bool ContainsAny(this string haystack, params string[] needles)
        {
            foreach (string needle in needles)
            {
                if (haystack.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Any(this string? source)
        {
            return string.IsNullOrWhiteSpace(source) == false;
        }
    }
}
