using System;
using Yarn;

#nullable enable

namespace Yarn
{
    /// <summary>
    /// The types of properties a node on a quest graph has.
    /// </summary>
    public enum QuestNodeStateProperty
    {
        /// <summary>
        /// The quest graph node is complete.
        /// </summary>
        Complete,
        /// <summary>
        /// The quest graph node is reachable.
        /// </summary>
        Reachable,
        /// <summary>
        /// The quest graph node is no longer needed.
        /// </summary>
        NoLongerNeeded,
        /// <summary>
        /// The quest graph node is currently active in its quest.
        /// </summary>
        Active
    }

    /// <summary>
    /// Contains methods for working with <see cref="Dialogue"/> objects and
    /// quest graphs.
    /// </summary>
    public static class QuestGraphExtensions
    {

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
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeStateProperty.Complete);
            });
            dialogue.Library.RegisterFunction("is_active", delegate (string questNodeName)
            {
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeStateProperty.Active);
            });
            dialogue.Library.RegisterFunction("is_reachable", delegate (string questNodeName)
            {
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeStateProperty.Reachable);
            });
            dialogue.Library.RegisterFunction("is_no_longer_needed", delegate (string questNodeName)
            {
                return GetQuestNodeState(dialogue, questNodeName, QuestNodeStateProperty.NoLongerNeeded);
            });
        }

        private static bool GetQuestNodeState(Dialogue dialogue, string questNodeName, QuestNodeStateProperty property)
        {
            var node = new QuestGraphNodeDescriptor(questNodeName);

            string stateType = property switch
            {
                QuestNodeStateProperty.Complete => "Complete",
                QuestNodeStateProperty.Reachable => "Reachable",
                QuestNodeStateProperty.NoLongerNeeded => "NoLongerNeeded",
                QuestNodeStateProperty.Active => "Active",
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
