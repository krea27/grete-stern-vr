using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    [MenuItem("Build/Build Android APK")]
    public static void BuildAndroid()
    {
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;

        string[] scenes = new string[] { "Assets/Scenes/SampleScene.unity" };
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Serena_Cardboard_Fixed.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log("[BuildScript] Iniciando compilación de Android...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BuildScript] Resultado de compilación: {report.summary.result}");
    }
}

