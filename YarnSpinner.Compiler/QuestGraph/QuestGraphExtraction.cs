using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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

        public static System.Text.Json.JsonDocument? ExtractQuestGraph(Project project)
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

            JsonElement SerializeNode(QuestGraphNodeDescriptor node)
            {
                return JsonSerializer.SerializeToElement(new
                {
                    id = node.Name,
                    quest = "Quest_" + node.Quest,
                    name = Utility.SplitCamelCase(node.Name),
                });
            }

            var stepsJSON = nodes
                .Where(n => n.Type == QuestGraphNodeDescriptor.NodeType.Step)
                .Select(SerializeNode);

            var tasksJSON = nodes
                .Where(n => n.Type == QuestGraphNodeDescriptor.NodeType.Task)
                .Select(SerializeNode);

            var questsJSON = nodes.Select(n => n.Quest).Distinct().Select(q => new
            {
                id = "Quest_" + q,
            }).Select(d => JsonSerializer.SerializeToElement(d));

            var edgesJSON = edges.Select(e =>
            {
                if (e.VariableName == null)
                {
                    return JsonSerializer.SerializeToElement(new
                    {
                        start = $"Quest_{e.FromNode.Quest}_{e.FromNode.Name}",
                        end = $"Quest_{e.ToNode.Quest}_{e.ToNode.Name}",
                    });
                }
                else
                {
                    return JsonSerializer.SerializeToElement(new
                    {
                        start = $"Quest_{e.FromNode.Quest}_{e.FromNode.Name}",
                        end = $"Quest_{e.ToNode.Quest}_{e.ToNode.Name}",
                        label = e.Description ?? "(no description)",
                        condition = e.VariableName,
                    });
                }
            });

            var variablesJSON = edges
                .Where(e => e.VariableName != null)
                .Select(e => new
                {
                    id = e.VariableName,
                    type = "bool",
                    description = e.Description ?? "(no description)",
                    source = e.VariableCreation == QuestGraphEdge.VariableType.Implicit ? "CreatedInEditor" : "ImportedFromYarnScript"
                });

            var documentJSON = JsonSerializer.SerializeToDocument(new
            {
                quests = questsJSON,
                goals = stepsJSON,
                tasks = tasksJSON,
                variables = variablesJSON,
                edges = edgesJSON,
            });

            return documentJSON;
        }
    }

}
