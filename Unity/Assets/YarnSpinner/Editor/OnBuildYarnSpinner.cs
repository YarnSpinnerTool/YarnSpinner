using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class OnBuildYarnSpinner : IPreprocessBuildWithReport, IPostprocessBuildWithReport {
    static bool createdResourcesDirectory = false;

    // Yarn's order in the build process is not important atm
    int IOrderedCallback.callbackOrder => int.MaxValue;

    void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report) {
        if (createdResourcesDirectory) {
            System.IO.Directory.Delete(GetResourcesPath(), true);
            System.IO.File.Delete(GetResourcesPath() + ".meta");
        }
    }

    void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report) {
        createdResourcesDirectory = false;
        if (!System.IO.Directory.Exists(GetResourcesPath())) {
            System.IO.Directory.CreateDirectory(GetResourcesPath());
            createdResourcesDirectory = true;
        }

        System.IO.File.Copy(ProjectSettings.SettingsPath, GetResourcesPath() + "/YarnProjectSettings.json", true);
    }

    static string GetResourcesPath () {
        return Application.dataPath + "/Resources";
    }
}
