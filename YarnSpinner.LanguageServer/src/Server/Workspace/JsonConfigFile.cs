using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace YarnLanguageServer
{
    internal class JsonConfigFile : IActionSource
    {
        List<Action> actions = new List<Action>();

        public JsonConfigFile(string text, bool IsBuiltIn)
        {
            try
            {
                var parsedConfig = JsonConvert.DeserializeObject<JsonConfigFormat>(text);

                foreach (var definition in parsedConfig.Functions) {
                    Action action = definition.ToAction();
                    action.IsBuiltIn = IsBuiltIn;
                    action.Type = ActionType.Function;
                    actions.Add(action);
                }

                foreach (var definition in parsedConfig.Commands) {
                    Action action = definition.ToAction();
                    action.IsBuiltIn = IsBuiltIn;
                    action.Type = ActionType.Command;
                    actions.Add(action);
                }
            }
            catch (Exception e) {

            }
        }

        public IEnumerable<Action> GetActions()
        {
            return actions;
        }

        internal class JsonConfigFormat
        {
            public List<RegisteredDefinition> Functions { get; set; }
            public List<RegisteredDefinition> Commands { get; set; }
        }
    }
}
