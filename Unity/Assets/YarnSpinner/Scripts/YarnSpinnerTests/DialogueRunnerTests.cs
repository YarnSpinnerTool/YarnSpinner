using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yarn.Unity;

public class DialogueRunnerTests
{
    [UnityTest]
    public IEnumerator HandleLine_OnValidYarnFile_SendCorrectLinesToUI()
    {
        SceneManager.LoadScene("DialogueRunnerTest");
        bool loaded = false;
        SceneManager.sceneLoaded += (index, mode) => {
            loaded = true;
        };
        yield return new WaitUntil(() => loaded);

        var runner = GameObject.FindObjectOfType<DialogueRunner>();
        runner.dialogueUI = runner.gameObject.AddComponent<DialogueRunnerMockUI>();
        DialogueRunnerMockUI dialogueUI = runner.dialogueUI as DialogueRunnerMockUI;

        runner.StartDialogue();
        yield return null;

        Assert.That(string.Equals(dialogueUI.CurrentLine, "Spieler: Kannst du mich hören?"));
        dialogueUI.MarkLineComplete();
        yield return null;
        Assert.That(string.Equals(dialogueUI.CurrentLine, "NPC: Klar und deutlich."));
    }
}
