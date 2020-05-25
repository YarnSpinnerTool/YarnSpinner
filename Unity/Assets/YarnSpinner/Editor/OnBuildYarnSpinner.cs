using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Steps required by YarnSpinner for a working build.
/// </summary>
public class OnBuildYarnSpinner : IPreprocessBuildWithReport, IPostprocessBuildWithReport {
    static bool createdResourcesDirectory = false;

    // Yarn's order in the build process is not important atm
    int IOrderedCallback.callbackOrder => int.MaxValue;

    void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report) {
        if (createdResourcesDirectory) {
            System.IO.Directory.Delete(GetResourcesPath(), true);
            System.IO.File.Delete(GetResourcesPath() + ".meta");
        } else {
            System.IO.File.Delete(GetResourcesPath() + "/YarnProjectSettings.json");
            System.IO.File.Delete(GetResourcesPath() + "/YarnProjectSettings.json.meta");
        }
    }

    void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report) {
        ProjectSettings.WriteProjectSettingsToDisk();

        createdResourcesDirectory = false;
        if (!System.IO.Directory.Exists(GetResourcesPath())) {
            System.IO.Directory.CreateDirectory(GetResourcesPath());
            createdResourcesDirectory = true;
        }

        var yarnProjectSettingsPath = Application.dataPath + "/../ProjectSettings" + "/YarnProjectSettings.json";
        System.IO.File.Copy(yarnProjectSettingsPath, GetResourcesPath() + "/YarnProjectSettings.json", true);
        AssetDatabase.ImportAsset("Assets/Resources/YarnProjectSettings.json", ImportAssetOptions.ForceUpdate);
    }

    static string GetResourcesPath () {
        return Application.dataPath + "/Resources";
    }
}
