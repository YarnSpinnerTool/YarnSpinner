using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System.Collections.Generic;

public class TestDialogueUIBehaviour : Yarn.Unity.DialogueUIBehaviour {

	private Queue<string> expectedLines = new Queue<string>();
	public void ExpectLine(string line) {
		expectedLines.Enqueue(line);
	}

	private Queue<string> expectedOptions = new Queue<string>();
	public void SelectOption(string option) {
		expectedOptions.Enqueue(option);
	}

	// Delegates that allow this behaviour to notify the test of events
	public delegate void LineHandler(Yarn.Line line);
	public LineHandler lineHandler;

	public delegate void OptionsHandler(Yarn.Options optionsCollection, Yarn.OptionChooser optionChooser);
	public OptionsHandler optionsHandler;

	public delegate void CommandHandler (Yarn.Command command);
	public CommandHandler commandHandler;


	public override System.Collections.IEnumerator RunLine (Yarn.Line line)
	{
		if (lineHandler != null)
			lineHandler(line);
		
		if (expectedLines.Count > 0) {
			Assert.AreEqual(expectedLines.Dequeue(), line.text);
		}

		yield break;
	}
	public override System.Collections.IEnumerator RunOptions (Yarn.Options optionsCollection, Yarn.OptionChooser optionChooser)
	{
		if (optionsHandler != null)
			optionsHandler(optionsCollection, optionChooser);

		if (expectedOptions.Count > 0) {
			var selection = expectedOptions.Dequeue();

			var index = optionsCollection.options.IndexOf(selection);

			Assert.AreNotEqual(index, -1, "Failed to find option \"{0}\"", selection);

			Assert.Less(index, optionsCollection.options.Count);
			optionChooser(index);
		}

		yield break;
	}
	public override System.Collections.IEnumerator RunCommand (Yarn.Command command)
	{
		if (commandHandler != null)
			commandHandler(command);
		yield break;
	}
}

public class YarnSpinnerTests {

	Yarn.Unity.DialogueRunner dialogueRunner;
	Yarn.Unity.VariableStorageBehaviour variableStorage;
	TestDialogueUIBehaviour dialogueUI;

	[SetUp]
	public void SetUp()
	{
		//Arrange

		// Create the dialogue runner
		var dialogueHost = new GameObject();
		dialogueRunner = dialogueHost.AddComponent<Yarn.Unity.DialogueRunner>();

		// Create the variable storage
		//variableStorage = dialogueHost.AddComponent<ExampleVariableStorage>();

		// Load the test script
		var text = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Yarn Spinner/Examples/Demo Assets/Space.json");
		dialogueRunner.AddScript(text);

		dialogueUI = dialogueHost.AddComponent<TestDialogueUIBehaviour>();
		dialogueRunner.dialogueUI = dialogueUI;

	}
	
    [Test]
    public void EditorTest()
    {
        
		dialogueUI.ExpectLine("Player: Hey, Sally.");
		dialogueUI.ExpectLine("Sally: Oh! Hi.");
		dialogueUI.ExpectLine("Sally: You snuck up on me.");
		dialogueUI.ExpectLine("Sally: Don't do that.");
		dialogueUI.SelectOption("Anything exciting happen on your watch?");

		dialogueRunner.StartDialogue("Sally");

		// Talking to Sally a second time should result in a different dialogue




        //Act
        //Try to rename the GameObject
        
        //Assert
        //The object has a new name
        //Assert.AreEqual(newGameObjectName, gameObject.name);
    }
}
