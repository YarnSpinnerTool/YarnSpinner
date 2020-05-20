using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yarn.Unity;

public class DialogueUITests
{
    [UnityTest]
    public IEnumerator RunLine_OnValidYarnLine_ShowCorrectText()
    {
        SceneManager.LoadScene("DialogueUITests");
        bool loaded = false;
        SceneManager.sceneLoaded += (index, mode) =>
        {
            loaded = true;
        };
        yield return new WaitUntil(() => loaded);

        var runner = GameObject.FindObjectOfType<DialogueRunner>();
        DialogueUI ui = runner.dialogueUI as DialogueUI;
        Text textCanvas = ui.dialogueContainer.transform.GetComponentsInChildren<Text>().First(element => element.gameObject.name == "Text");

        // Arrange
        runner.StartDialogue();
        float startTime;
        startTime = Time.time;
        while (Time.time - startTime < 10 && !string.Equals(textCanvas.text, "Spieler: Kannst du mich hören?"))
        {
            yield return null;
        }

        Assert.That(string.Equals(textCanvas.text, "Spieler: Kannst du mich hören?"));

        // Arrange for second line
        yield return null;
        ui.MarkLineComplete();

        startTime = Time.time;
        while (Time.time - startTime < 10 && !string.Equals(textCanvas.text, "NPC: Klar und deutlich."))
        {
            yield return null;
        }

        Assert.That(string.Equals(textCanvas.text, "NPC: Klar und deutlich."));

        // Cleanup
        yield return null;
        ui.MarkLineComplete();
        yield return null;
        ui.SelectOption(0);
        yield return null;
    }
}
