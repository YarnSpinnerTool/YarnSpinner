using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace YarnLanguageServer
{
    internal class JsonConfigFile : IFunctionDefinitionsProvider
    {
        public Dictionary<string, RegisteredFunction> FunctionDefinitions { get; set; }

        public JsonConfigFile(string text, Uri uri)
        {
            FunctionDefinitions = new Dictionary<string, RegisteredFunction>();
            try
            {
                var parsedConfig = JsonConvert.DeserializeObject<JsonConfigFormat>(text);
                parsedConfig.Functions.ForEach(f =>
                {
                    f.DefinitionFile = uri;

                    if (f.Parameters != null && !f.MinParameterCount.HasValue) {
                        f.MinParameterCount = f.Parameters.Count(p => p.DefaultValue == null && !p.IsParamsArray);
                    }

                    if (f.Parameters != null && !f.MaxParameterCount.HasValue)
                    {
                        f.MaxParameterCount = f.Parameters.Any(p => p.IsParamsArray) ? null : f.Parameters.Count();
                    }

                    FunctionDefinitions[f.DefinitionName] = f;
                    var worked = FunctionDefinitions[f.DefinitionName].DefinitionFile == uri;
                });
            }
            catch (Exception) { }
        }

        internal class JsonConfigFormat
        {
            public List<RegisteredFunction> Functions { get; set; }
        }
    }
}