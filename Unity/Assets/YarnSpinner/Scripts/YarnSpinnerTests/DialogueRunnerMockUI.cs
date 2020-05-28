using System;
using System.Collections;
using Yarn.Unity;

public class DialogueRunnerMockUI : Yarn.Unity.DialogueViewBase
{
    public string CurrentLine { get; private set; } = default;

    protected override IEnumerator RunLine(LocalizedLine dialogueLine)
    {
        CurrentLine = dialogueLine.TextLocalized;
        yield break;
    }

    public override void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected)
    {
        // Do nothing
    }

    protected override void FinishCurrentLine()
    {
        // Do nothing
    }

    protected override IEnumerator EndCurrentLine()
    {
        // Do nothing
        yield break;
    }

    protected override void OnFinishedLineOnAllViews()
    {
        // Do nothing
    }
}
