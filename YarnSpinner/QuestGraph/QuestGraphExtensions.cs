using System;
using Yarn;

#nullable enable

namespace Yarn
{
    /// <summary>
    /// Contains methods for working with <see cref="Dialogue"/> objects and
    /// quest graphs.
    /// </summary>
    public static class QuestGraphExtensions
    {
        private enum QuestNodeProperty
        {
            Complete, Reachable, NoLongerNeeded, Active
        }

        /// <summary>
        /// Registers functions for querying the state of quest graph nodes into
        /// <paramref name="dialogue"/>'s library.
        /// </summary>
        /// <param name="dialogue">The dialogue to register functions into.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref
        /// name="dialogue"/> is <see langword="null"/>.</exception>
        public static void AddQuestGraphSupport(this Dialogue dialogue)
        {
            if (dialogue == null)
            {
                throw new ArgumentNullException(nameof(dialogue));
            }

            dialogue.Library.RegisterFunction("is_complete", delegate (string questNodeName)
            {
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeProperty.Complete);
            });
            dialogue.Library.RegisterFunction("is_active", delegate (string questNodeName)
            {
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeProperty.Active);
            });
            dialogue.Library.RegisterFunction("is_reachable", delegate (string questNodeName)
            {
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeProperty.Reachable);
            });
            dialogue.Library.RegisterFunction("is_no_longer_needed", delegate (string questNodeName)
            {
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeProperty.NoLongerNeeded);
            });
        }

        private static bool GetQuestNodeState(Dialogue dialogue, string questNodeName, QuestNodeProperty property)
        {
            var node = new QuestGraphNodeDescriptor(questNodeName);

            string stateType = property switch
            {
                QuestNodeProperty.Complete => "Complete",
                QuestNodeProperty.Reachable => "Reachable",
                QuestNodeProperty.NoLongerNeeded => "NoLongerNeeded",
                QuestNodeProperty.Active => "Active",
                _ => throw new ArgumentOutOfRangeException(nameof(property)),
            };

            var variableName = $"$Quest_{node.Quest}_{node.Name}_{stateType}";

            if (dialogue.TryGetSmartVariable(variableName, out bool result))
            {
                return result;
            }
            else
            {
                throw new ArgumentException("Unknown quest node " + questNodeName);
            }
        }
    }
}
