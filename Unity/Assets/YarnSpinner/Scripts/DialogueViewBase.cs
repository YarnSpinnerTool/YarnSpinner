using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yarn.Unity
{
    /// <summary>
    /// A <see cref="MonoBehaviour"/> that can present the data of a dialogue executed by a <see cref="DialogueRunner"/> to the user. The <see cref="DialogueRunner"/> uses subclasses of this type to relay information to and from the user, and to pause and resume the execution of the <see cref="YarnProgram"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The term "view" is meant in the broadest sense, e.g. a view on the dialogue (MVVM pattern). Therefore, this abstract class only defines how a specific view on the dialogue should communicate with the <see cref="DialogueRunner"/> (e.g. display text or trigger a voice over clip). How to present the content to the user will be the responsibility of all classes inheriting from this class.
    /// </para>
    /// <para>
    /// The inheriting classes will receive a <see cref="LocalizedLine"/> and can be in one of the stages defined in <see cref="DialogueLineStatus"/> while presenting it.
    /// </para>
    /// </remarks>
    /// <seealso cref="DialogueRunner.dialogueViews"/>
    /// <seealso cref="DialogueUI"/>
    public abstract class DialogueViewBase : MonoBehaviour
    {
        /// <summary>
        /// The status of the <see cref="LocalizedLine"/> currently handled by this <see cref="DialogueViewBase"/> instance.
        /// </summary>
        public enum DialogueLineStatus {
            /// <summary>
            /// The line is being build up and shown to the user.
            /// </summary>
            Running,
            /// <summary>
            /// The line got interrupted while being build up and should complete showing the line asap. View classes should get to the end of the line as fast as possible. A view class showing text would stop building up the text and immediately show the entire line and a view class playing voice over clips would do a very quick fade out and stop playback afterwards.
            /// </summary>
            Interrupted,
            /// <summary>
            /// The line has been fully presented to the user. A view class presenting the line as text would be showing the entire line and a view class playing voice over clips would be silent now.
            /// </summary>
            /// <remarks>
            /// A line that was previously <see cref="DialogueLineStatus.Interrupted"/> will become <see cref="DialogueLineStatus.Finished"/> once the <see cref="DialogueViewBase"/> has completed the interruption process.
            /// </remarks>
            Finished,
            /// <summary>
            /// The line is about to go away. A view class presenting the line as text would stop showing the line to the user.
            /// </summary>
            Ending,
            /// <summary>
            /// The line is not being presented anymore in any way to the user.
            /// </summary>
            Ended
        }
        internal DialogueLineStatus dialogueLineStatus;

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
        /// should not call the <paramref name="onDialogueLineFinished"/>
        /// method.
        /// </remarks>
        /// <param name="dialogueLine">The content of the line that should be presented to the user.</param>
        /// <param name="onDialogueLineFinished">The method that should be called after the line has been
        /// finished.</param>
        /// <returns>Returns <see cref="Dialogue.HandlerExecutionType.PauseExecution"/> if dialogue should
        /// wait until the completion handler is called before continuing execution;
        /// <see cref="Dialogue.HandlerExecutionType.ContinueExecution"/> if dialogue should immediately
        /// continue running after calling this method.</returns>
        /// FIXME: If this method is expected to be called only from the DialogueRunner
        /// then this should be converted into a coroutine and merged with RunLineWithCallback();
        public void RunLine(LocalizedLine dialogueLine, Action onDialogueLineFinished) {
            StartCoroutine(RunLineWithCallback(dialogueLine, onDialogueLineFinished));
        }
        /// <summary>
        /// Run the given <see cref="LocalizedLine"/> on the derived class. The implementation
        /// will take care of presenting the line to the user e.g. by showing the text or playing 
        /// the voice over.
        /// </summary>
        /// <param name="dialogueLine">The content of the current line that should be presented to the user.</param>
        /// <returns></returns>
        protected abstract IEnumerator RunLine(LocalizedLine dialogueLine);

        private IEnumerator RunLineWithCallback (LocalizedLine dialogueLine, Action onDialogueLineFinished) {
            dialogueRunnerCurrentLine = onDialogueLineFinished.Target as DialogueRunner;
            dialogueLineStatus = DialogueLineStatus.Running;

            yield return StartCoroutine(RunLine(dialogueLine));

            dialogueLineStatus = DialogueLineStatus.Finished;
            onDialogueLineFinished();
        }

        /// <summary>
        /// Finish presenting the current <see cref="LocalizedLine"/>.
        /// </summary>
        /// <remarks>
        /// Called if the <see cref="DialogueRunner"/> has received the user's request on one of the 
        /// <see cref="DialogueRunner.dialogueViews"/> classes derived from <see cref="DialogueViewBase"/> to 
        /// finish presenting the current line.
        /// </remarks>
        internal void FinishRunningCurrentLine() {
            dialogueLineStatus = DialogueLineStatus.Interrupted;
            FinishCurrentLine();
        }

        /// <summary>
        /// Finish presenting the current <see cref="LocalizedLine"/> on the derived class.
        /// </summary>
        /// <remarks>
        /// It is OK to apply quick fade outs. Because this is not a coroutine, it is advisable to do that
        /// inside of the active <see cref="DialogueViewBase.RunLine(LocalizedLine)"/> coroutine on the
        /// derived class. In that case, this method should only inform the active coroutine on the
        /// derived class to quickly finish presenting the current line.
        /// </remarks>
        protected abstract void FinishCurrentLine();

        /// <summary>
        /// Called by the <see cref="DialogueRunner"/> to inform all classes inheriting from <see cref="DialogueViewBase"/> that they all finished presenting the current <see cref="LocalizedLine"/> to the user.
        /// </summary>
        /// <remarks>
        /// This can be used to trigger sounds in sync when a line has been fully presented or to show the user a prompt to go to the next line.
        /// </remarks>
        protected internal abstract void OnFinishedLineOnAllViews();

        /// <summary>
        /// End the current <see cref="LocalizedLine"/>.
        /// </summary>
        /// <remarks>
        /// This should only be called from  the <see cref="DialogueRunner"/> if all <see 
        /// cref="DialogueRunner.dialogueViews"/> have finished presenting the current line (e.g. finished
        /// quick fade outs).
        /// </remarks>
        /// <param name="onDialogueLineCompleted"></param>
        /// <returns></returns>
        internal IEnumerator EndCurrentLine(Action onDialogueLineCompleted) {
            dialogueRunnerCurrentLine = onDialogueLineCompleted.Target as DialogueRunner;
            dialogueLineStatus = DialogueLineStatus.Ending;

            yield return StartCoroutine(EndCurrentLine());

            dialogueLineStatus = DialogueLineStatus.Ended;
            onDialogueLineCompleted();
        }
        /// <summary>
        /// End the current <see cref="LocalizedLine"/> on the derived class.
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerator EndCurrentLine();

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
        /// Called by the <see cref="DialogueRunner"/> to signal that the end of a node has been reached.
        /// </summary>
        /// <remarks>
        /// This method may be called multiple times before <see cref="DialogueComplete"/> is called.
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

        /// <summary>
        /// Signals that the user wants to go to the next line.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is generally called by a "continue" button, and
        /// causes the DialogueUI to signal the <see
        /// cref="DialogueRunner"/> to proceed to the next piece of
        /// content.
        /// </para>
        /// <para>
        /// If this method is called before the line has finished appearing
        /// (that is, before the status changes to 
        /// <see cref="DialogueLineStatus.Finished"/>), the 
        /// <see cref="DialogueRunner"/> will call <see cref="FinishRunningCurrentLine"/> 
        /// on all <see cref="DialogueViewBase"/> derived classes and wait for them to finish
        /// before calling <see cref="EndCurrentLine(Action)"/> on all of them.
        /// </para>
        /// </remarks>
        public void MarkLineComplete()
        {
            if (dialogueRunnerCurrentLine) {
                dialogueRunnerCurrentLine.OnViewUserIntentNextLine();
            }
        }

        /// <summary>
        /// Signals that the user wants to go to the end of the current line.
        /// </summary>
        public void FinishLine() {
            if (dialogueRunnerCurrentLine) {
                dialogueRunnerCurrentLine.OnViewUserIntentFinishLine();
            }
        }
    }
}
