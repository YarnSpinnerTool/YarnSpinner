using System;
using System.Collections.Generic;
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