using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn;
using Yarn.Unity;

public class DialogueRunnerMockUI : Yarn.Unity.DialogueUIBehaviour
{
    public string CurrentLine { get; private set; } = default;
    private Action onComplete = default;

    public override Dialogue.HandlerExecutionType RunCommand(Command command, Action onCommandComplete)
    {
        return Dialogue.HandlerExecutionType.ContinueExecution;
    }

    public override Dialogue.HandlerExecutionType RunLine(Line line, ILineLocalisationProvider localisationProvider, Action onLineComplete)
    {
        CurrentLine = localisationProvider.GetLocalisedTextForLine(line);
        onComplete = onLineComplete;
        return Dialogue.HandlerExecutionType.PauseExecution;
    }

    public void MarkLineComplete()
    {
        onComplete?.Invoke();
    }

    public override void RunOptions(OptionSet optionSet, ILineLocalisationProvider localisationProvider, Action<int> onOptionSelected)
    {
        // Do nothing
    }
}
