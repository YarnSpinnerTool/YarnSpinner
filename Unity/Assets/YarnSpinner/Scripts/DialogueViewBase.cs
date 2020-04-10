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
    /// The inhereting classes will receive a DialogueLine and are responsible for presenting it to the user. A DialogueLine can be in the following possible stages:
    ///   * started
    ///   * interrupted (line was in the middle of playing/appearing but the user indicated to proceed to the next line. Views should get to the end as fast as possible (fade out voice over, show full line)
    ///   * completed (finished presenting the line, i.e. full text of the line or finished playback of voice over audio) -> could be renamed to FinishedDisplaying
    ///   * ended (the user has indicated to proceed to the next line)
    /// FIXME: Currently, only the completed-request is implemented in the MVVM pattern. Started and Ended likely don't need to.
    /// TODO: Implement "interrupted" like "completed"
    /// TODO: Determine if the line stages should be implemented as states or if it's enough to have the necessary calls to change the states implicitly.
    /// </remarks>
    /// <seealso cref="DialogueRunner.dialogueViews"/>
    /// <seealso cref="DialogueUI"/>
    public abstract class DialogueViewBase : MonoBehaviour 
    {
        /// <summary>
        /// The instance of the current line's DialogueRunner
        /// </summary>
        internal DialogueRunner dialogueRunnerCurrentLine;

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
        /// FIXME: If this method is expected to be called only from the DialogueRunner
        /// then this should be converted into a coroutine and merged with RunLineWithCallback();
        public void RunLine(DialogueLine dialogueLine, Action<DialogueViewBase> onLineComplete) {
            StartCoroutine(RunLineWithCallback(dialogueLine, onLineComplete));
        }
        internal abstract IEnumerator RunLine(DialogueLine dialogueLine);

        private IEnumerator RunLineWithCallback (DialogueLine dialogueLine, Action<DialogueViewBase> onLineComplete) {
            dialogueRunnerCurrentLine = onLineComplete.Target as DialogueRunner;

            yield return StartCoroutine(RunLine(dialogueLine));

            onLineComplete(this);
        }

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
        /// FIXME: This doesn't seem to be called anymore ...?
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

        /// <summary>
        /// Signals that the user has finished with a line, or wishes to
        /// skip to the end of the current line.
        /// </summary>
        /// <remarks>
        /// This method is generally called by a "continue" button, and
        /// causes the DialogueUI to signal the <see
        /// cref="DialogueRunner"/> to proceed to the next piece of
        /// content.
        ///
        /// If this method is called before the line has finished appearing
        /// (that is, before <see cref="onLineFinishDisplaying"/> is
        /// called), the DialogueUI immediately displays the entire line
        /// (via the <see cref="onLineUpdate"/> method), and then calls
        /// <see cref="onLineFinishDisplaying"/>.
        /// </remarks>
        public void MarkLineComplete() 
        {
            dialogueRunnerCurrentLine?.OnUserViewRequestedLineComplete();
        }

        /// <summary>
        /// The <see cref="DialogueRunner"/> has received the user's request on a view class derived from <see cref="DialogueViewBase"/> to complete the current line.
        /// </summary>
        internal abstract void OnMarkLineComplete();
    }
}
