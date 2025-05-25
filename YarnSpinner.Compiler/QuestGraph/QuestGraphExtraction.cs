using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

#nullable enable

namespace Yarn.Compiler
{
    public static class QuestGraphExtraction
    {
        public static IEnumerable<QuestGraphEdge> ExtractEdges(FileParseResult file)
        {
            var visitor = new QuestGraphVisitor(file, Project.CurrentProjectFileVersion, new List<Diagnostic>(), new List<Declaration>());
            visitor.Visit(file.Tree);
            return visitor.Edges;
        }

        public static string? ExtractQuestGraphAsJson(Project project, string existingQuestGraph)
        {
            var existingQuestGraphDocument = JsonDocument.Parse(existingQuestGraph);
            return System.Text.Json.JsonSerializer.Serialize(ExtractQuestGraph(project, existingQuestGraphDocument));
        }

        public static System.Text.Json.JsonDocument? ExtractQuestGraph(Project project, JsonDocument? existingQuestGraph)
        {

            var job = CompilationJob.CreateFromFiles(project.SourceFiles);
            job.LanguageVersion = project.FileVersion;
            job.CompilationType = CompilationJob.Type.StringsOnly;
            var result = Compiler.Compile(job);

            if (result.ContainsErrors)
            {
                return null;
            }

            var edges = new List<QuestGraphEdge>(result.QuestGraphEdges).Distinct();

            var nodes =
                edges.SelectMany(e => new[] { e.FromNode, e.ToNode }).Distinct()
                ;

            var document = existingQuestGraph != null
                ? (JsonObject.Create(existingQuestGraph.RootElement) ?? new JsonObject())
                : new JsonObject();

            JsonArray GetOrCreateArray(string name)
            {
                var array = document[name]?.AsArray();
                if (array == null)
                {
                    array = new JsonArray();
                    document[name] = array;
                }
                return array;
            }

            var questsJSON = GetOrCreateArray("quests");
            var tasksJSON = GetOrCreateArray("tasks");
            var goalsJSON = GetOrCreateArray("goals");
            var variablesJSON = GetOrCreateArray("variables");
            var edgesJSON = GetOrCreateArray("edges");
            var rulesJSON = GetOrCreateArray("rules");

            static JsonObject GetOrCreateElement(JsonArray array, System.Func<JsonNode, bool> predicate)
            {
                JsonObject? element = array.Where(i => i != null).FirstOrDefault(predicate!)?.AsObject();
                if (element == null)
                {
                    element = new JsonObject();
                    array.Add(element);
                }
                return element;
            }

            static void SetValueIfNotExists(JsonObject obj, string name, JsonNode value)
            {
                if (obj.ContainsKey(name))
                {
                    return;
                }
                obj[name] = value;
            }


            (JsonArray, IEnumerable<QuestGraphNodeDescriptor>)[] nodePairs = new[] {
                    (goalsJSON, nodes.Where(n => n.Type == QuestGraphNodeDescriptor.NodeType.Step)),
                    (tasksJSON, nodes.Where(n => n.Type == QuestGraphNodeDescriptor.NodeType.Task)),
                };

            foreach (var (array, nodesToAdd) in nodePairs)
            {
                foreach (var nodeToAdd in nodesToAdd)
                {
                    var nodeJSON = GetOrCreateElement(array,
                        g => g["quest"]?.GetValue<string>() == "Quest_" + nodeToAdd.Quest
                            && g["id"]?.GetValue<string>() == nodeToAdd.Name
                    );

                    nodeJSON["quest"] = "Quest_" + nodeToAdd.Quest;
                    nodeJSON["id"] = nodeToAdd.Name;
                    SetValueIfNotExists(nodeJSON, "name", Utility.SplitCamelCase(nodeToAdd.Name));
                }
            }


            foreach (var questName in nodes.Select(n => n.Quest).Distinct())
            {
                var questJSON = GetOrCreateElement(questsJSON, q => q["id"]?.GetValue<string>() == "Quest_" + questName);
                questJSON["id"] = "Quest_" + questName;
            }

            foreach (var edgeData in edges)
            {
                var fromNodeID = $"Quest_{edgeData.FromNode.Quest}_{edgeData.FromNode.Name}";
                var toNodeID = $"Quest_{edgeData.ToNode.Quest}_{edgeData.ToNode.Name}";

                var edgeJSON = GetOrCreateElement(edgesJSON,
                    e => e["start"]?.GetValue<string>() == fromNodeID
                        && e["end"]?.GetValue<string>() == toNodeID
                    );

                edgeJSON["start"] = fromNodeID;
                edgeJSON["end"] = toNodeID;

                if (edgeData.VariableName != null)
                {
                    edgeJSON["condition"] = edgeData.VariableName;
                    SetValueIfNotExists(edgeJSON, "label", edgeData.Description ?? "(no description)");

                    var variableJSON = GetOrCreateElement(variablesJSON, v => v["id"]?.GetValue<string>() == edgeData.VariableName);

                    variableJSON["id"] = edgeData.VariableName;
                    variableJSON["type"] = "bool";
                    variableJSON["source"] = edgeData.VariableCreation == QuestGraphEdgeDescriptor.VariableType.Implicit ? "CreatedInEditor" : "ImportedFromYarnScript";

                    SetValueIfNotExists(variableJSON, "description", edgeData.Description ?? "(no description)");

                    if (edgeData.VariableCreation == QuestGraphEdgeDescriptor.VariableType.Implicit)
                    {
                        // Create a rule that this variable is only true if its
                        // source node is reachable (i.e. the only way that it
                        // becomes true is when it completes the parent)
                        var ruleJSON = GetOrCreateElement(rulesJSON, r =>
                        {
                            var condition = r["condition"]?.AsObject();
                            if (condition == null) { return false; }
                            var implies = condition["implies"]?.AsArray();
                            if (implies == null) { return false; }

                            var variable = implies[0]?.GetValue<string>();

                            var nodeConstraintObj = implies[1]?.AsObject()["node"];
                            var nodeConstraint = nodeConstraintObj?.GetValue<string>();


                            // return false;
                            return variable == edgeData.VariableName && nodeConstraint == edgeData.FromNode.FullName;
                        });

                        ruleJSON["name"] = ruleJSON["name"] ?? edgeData.FromNode.FullName;

                        ruleJSON["condition"] = JsonObject.Parse(@$"{{""implies"":[
                            ""{edgeData.VariableName}"",
                            {{
                                ""node"":""{edgeData.FromNode.FullName}"",
                                ""state"": ""Reachable""
                            }}
                        ]}}");

                    }
                }
            }

            return JsonSerializer.SerializeToDocument(document);
        }
    }

}
