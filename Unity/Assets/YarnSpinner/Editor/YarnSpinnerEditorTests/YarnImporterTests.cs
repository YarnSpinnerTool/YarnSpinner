using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.TestTools;

public class YarnImporterTests
{
    [Test]
    public void YarnImporter_OnValidYarnFile_ShouldCompile()
    {
        const string textYarnAsset = "title: Start\ntags:\ncolorID: 0\nposition: 0,0\n--- \nSpieler: Kannst du mich hören? #line:0e3dc4b\nNPC: Klar und deutlich. #line:0967160\n[[Mir reicht es.| Exit]] #line:04e806e\n[[Nochmal!|Start]] #line:0901fb2\n===\ntitle: Exit\ntags: \ncolorID: 0\nposition: 0,0\n--- \n===";
        string fileName = Path.GetRandomFileName();

        File.WriteAllText(Application.dataPath + "/" + fileName + ".yarn", textYarnAsset);
        AssetDatabase.Refresh();
        var result = ScriptedImporter.GetAtPath("Assets/" + fileName + ".yarn") as YarnImporter;

        Assert.That(result.isSuccesfullyCompiled);

        AssetDatabase.DeleteAsset("Assets/" + fileName + ".yarn");
    }

    [Test]
    public void YarnImporter_OnInvalidYarnFile_ShouldNotCompile()
    {
        const string textYarnAsset = "This is not a valid yarn file and thus compilation should fail.";
        string fileName = Path.GetRandomFileName();

        File.WriteAllText(Application.dataPath + "/" + fileName + ".yarn", textYarnAsset);
        LogAssert.ignoreFailingMessages = true;
        AssetDatabase.Refresh();
        LogAssert.ignoreFailingMessages = false;
        var result = ScriptedImporter.GetAtPath("Assets/" + fileName + ".yarn") as YarnImporter;

        Assert.That(!result.isSuccesfullyCompiled);

        AssetDatabase.DeleteAsset("Assets/" + fileName + ".yarn");
    }

    [Test]
    public void YarnImporter_OnValidYarnFile_GetExpectedStrings()
    {
        const string textYarnAsset = "title: Start\ntags:\ncolorID: 0\nposition: 0,0\n--- \nSpieler: Kannst du mich hören? #line:0e3dc4b\nNPC: Klar und deutlich. #line:0967160\n[[Mir reicht es.| Exit]] #line:04e806e\n[[Nochmal!|Start]] #line:0901fb2\n===\ntitle: Exit\ntags: \ncolorID: 0\nposition: 0,0\n--- \n===";
        string fileName = Path.GetRandomFileName();
        string expectedStringTable = "id,text,file,node,lineNumber\r\nline:0e3dc4b,Spieler: Kannst du mich hören?," + fileName + ",Start,6\r\nline:0967160,NPC: Klar und deutlich.," + fileName + ",Start,7\r\nline:04e806e,Mir reicht es.," + fileName + ",Start,8\r\nline:0901fb2,Nochmal!," + fileName + ",Start,9\r\n";

        File.WriteAllText(Application.dataPath + "/" + fileName + ".yarn", textYarnAsset);
        AssetDatabase.Refresh();
        var result = ScriptedImporter.GetAtPath("Assets/" + fileName + ".yarn") as YarnImporter;

        Assert.That(Equals(result.baseLanguage.text, expectedStringTable));

        AssetDatabase.DeleteAsset("Assets/" + fileName + ".yarn");
    }

    [Test]
    public void YarnEditorUtility_HasValidEditorResources() {

        // Test that YarnEditorUtility can locate the editor assets
        Assert.IsNotNull(YarnEditorUtility.GetYarnDocumentIconTexture());
        Assert.IsNotNull(YarnEditorUtility.GetTemplateYarnScriptPath());
    }
}
