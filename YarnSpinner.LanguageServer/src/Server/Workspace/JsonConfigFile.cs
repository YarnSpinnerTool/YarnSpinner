using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace YarnLanguageServer
{
    internal class JsonConfigFile : IActionSource
    {
        private readonly List<Action> actions = new List<Action>();

        public JsonConfigFile(string text, bool isBuiltIn)
        {
            var parsedConfig = JsonConvert.DeserializeObject<JsonConfigFormat>(text);

            foreach (var definition in parsedConfig.Functions) {
                Action action = definition.ToAction();
                action.IsBuiltIn = isBuiltIn;
                action.Type = ActionType.Function;
                actions.Add(action);
            }

            foreach (var definition in parsedConfig.Commands) {
                Action action = definition.ToAction();
                action.IsBuiltIn = isBuiltIn;
                action.Type = ActionType.Command;
                actions.Add(action);
            }
        }

        public IEnumerable<Action> GetActions()
        {
            return actions;
        }

        internal class JsonConfigFormat
        {
            public List<RegisteredDefinition> Functions { get; set; } = new ();
            public List<RegisteredDefinition> Commands { get; set; } = new ();
        }
    }
}
