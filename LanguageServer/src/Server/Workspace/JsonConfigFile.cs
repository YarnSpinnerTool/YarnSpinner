using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace YarnLanguageServer
{
    internal class JsonConfigFile : IDefinitionsProvider
    {
        public Dictionary<string, RegisteredDefinition> Definitions { get; set; }

        public JsonConfigFile(string text, Uri uri, Workspace workspace, bool IsBuiltIn)
        {
            Definitions = new Dictionary<string, RegisteredDefinition>();
            try
            {
                var parsedConfig = JsonConvert.DeserializeObject<JsonConfigFormat>(text);
                parsedConfig.Functions?.ForEach(f => RegisterDefinition(f, false,uri, workspace, IsBuiltIn));
                parsedConfig.Commands?.ForEach(f => RegisterDefinition(f, true, uri, workspace, IsBuiltIn));
            }
            catch (Exception) { }
        }

        private void RegisterDefinition(RegisteredDefinition f, bool isCommand, Uri uri, Workspace workspace, bool IsBuiltIn)
        {
            f.DefinitionFile = uri;
            f.IsCommand = isCommand;
            f.IsBuiltIn = IsBuiltIn;
            f.Priority = 0;
            if (f.Parameters != null && !f.MinParameterCount.HasValue)
            {
                f.MinParameterCount = f.Parameters.Count(p => p.DefaultValue == null && !p.IsParamsArray);
            }

            if (f.Parameters != null && !f.MaxParameterCount.HasValue)
            {
                f.MaxParameterCount = f.Parameters.Any(p => p.IsParamsArray) ? null : f.Parameters.Count();
            }

            if (string.IsNullOrEmpty(f.Language)) { f.Language = "text"; }

            if (!workspace.Configuration.CSharpLookup && f.Language == Utils.CSharpLanguageID) { return; }
            Definitions[f.YarnName] = f;

        }

        internal class JsonConfigFormat
        {
            public List<RegisteredDefinition> Functions { get; set; }
            public List<RegisteredDefinition> Commands { get; set; }
        }
    }
}