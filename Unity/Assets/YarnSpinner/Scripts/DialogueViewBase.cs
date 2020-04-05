using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yarn.Unity 
{

    /// <summary>
    /// A <see cref="MonoBehaviour"/> that can present the data of a dialogue. The term "view" is meant in the broadest sense, e.g. a view on the dialogue (MVVM pattern). Therefore, tis abstract class only defines how a specific view on the dialogue should communicate with the Runner (e.g. display a line or trigger a voice over clip). How to present the content to the user will be the responsibility of all classes inhereting from this class.
    /// </summary>
    /// <remarks>
    /// The <see cref="DialogueRunner"/> uses subclasses of this type to relay information to and from the user, and to pause and resume the execution of the Yarn program.
    /// </remarks>
    /// <seealso cref="DialogueRunner.dialogueViews"/>
    /// <seealso cref="DialogueUI"/>
    public abstract class DialogueViewBase : MonoBehaviour 
    {
        /// <summary>Signals that a conversation has started.</summary>
        public virtual void DialogueStarted() 
        {
            // Default implementation does nothing.
        }

        /// <summary>
        /// Called by the <see cref="DialogueRunner"/> to signal that a line should be displayed to the user.
        /// </summary>
        /// <remarks>
        /// If this method returns <see
        /// cref="Dialogue.HandlerExecutionType.ContinueExecution"/>, it
        /// should not not call the <paramref name="onLineComplete"/>
        /// method.
        /// </remarks>
        /// <param name="dialogueLine">The line that should be displayed to the
        /// user.</param>
        /// <param name="onLineComplete">A method that should be called to
        /// indicate that the line has finished being delivered.</param>
        /// <returns><see
        /// cref="Dialogue.HandlerExecutionType.PauseExecution"/> if
        /// dialogue should wait until the completion handler is
        /// called before continuing execution; <see
        /// cref="Dialogue.HandlerExecutionType.ContinueExecution"/> if
        /// dialogue should immediately continue running after calling this
        /// method.</returns>
        public abstract void RunLine(DialogueLine dialogueLine, Action<DialogueViewBase> onLineComplete);

        /// <summary>
        /// Called by the <see cref="DialogueRunner"/> to signal that a set of options should be displayed to the user.
        /// </summary>
        /// <remarks>
        /// When this method is called, the <see cref="DialogueRunner"/>
        /// will pause execution until the `onOptionSelected` method is
        /// called.
        /// </remarks>
        /// <param name="dialogueOptions">The set of options that should be
        /// displayed to the user.</param>
        /// <param name="onOptionSelected">A method that should be called
        /// when the user has made a selection.</param>
        public abstract void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected);

        /// <summary>
        /// Called by the <see cref="DialogueRunner"/> to signal that a command should be executed.
        /// </summary>
        /// <remarks>
        /// This method will only be invoked if the <see cref="Command"/>
        /// could not be handled by the <see cref="DialogueRunner"/>.
        ///
        /// If this method returns <see
        /// cref="Dialogue.HandlerExecutionType.ContinueExecution"/>, it
        /// should not call the <paramref name="onCommandComplete"/>
        /// method.
        /// </remarks>
        /// <param name="command">The command to be executed.</param>
        /// <param name="onCommandComplete">A method that should be called
        /// to indicate that the DialogueRunner should continue
        /// execution.</param>
        /// <inheritdoc cref="RunLine(Line, ILineLocalisationProvider, Action)"/>
        public abstract Dialogue.HandlerExecutionType RunCommand(Command command, Action onCommandComplete);

        /// <summary>
        /// Called by the <see cref="DialogueRunner"/> to signal that the end of a node has been reached.
        /// </summary>
        /// <remarks>
        /// This method may be called multiple times before <see cref="DialogueComplete"/> is called.
        /// 
        /// If this method returns <see
        /// cref="Dialogue.HandlerExecutionType.ContinueExecution"/>, do
        /// not call the <paramref name="onComplete"/> method.
        /// </remarks>
        /// <param name="nextNode">The name of the next node that is being entered.</param>
        /// <param name="onComplete">A method that should be called to
        /// indicate that the DialogueRunner should continue executing.</param>
        /// <inheritdoc cref="RunLine(Line, ILineLocalisationProvider, Action)"/>
        public virtual Dialogue.HandlerExecutionType NodeComplete(string nextNode, Action onComplete) 
        {
            // Default implementation does nothing.
            return Dialogue.HandlerExecutionType.ContinueExecution;
        }

        /// <summary>
        /// Called by the <see cref="DialogueRunner"/> to signal that the dialogue has ended.
        /// </summary>
        public virtual void DialogueComplete() 
        {
            // Default implementation does nothing.
        }

        public abstract void VoiceOverDuration(float duration);
    }
}
