using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class Builder
{
    static Builder()
    {
        ForzarModoRelease();
    }

    [MenuItem("Serena VR/1. Forzar Modo Release (Desactivar Consola)", false, 1)]
    public static void ForzarModoRelease()
    {
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
        Debug.Log("[Serena VR] Modo Release asegurado: Development Build desactivado automáticamente.");
    }

    [MenuItem("Serena VR/2. Compilar APK Release Limpio", false, 2)]
    public static void CompilarApkReleaseMenu()
    {
        ForzarModoRelease();
        BuildAndroid();
    }

    public static void BuildAndroid()
    {
        ForzarModoRelease();
        Debug.Log("[Builder] Iniciando compilación Release limpia de APK para Serena VR...");

        string carpetaBuilds = "Builds";
        if (!Directory.Exists(carpetaBuilds))
        {
            Directory.CreateDirectory(carpetaBuilds);
        }

        BuildPlayerOptions opt = new BuildPlayerOptions();
        opt.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        opt.locationPathName = Path.Combine(carpetaBuilds, "Serena_Cardboard_Release.apk");
        opt.target = BuildTarget.Android;
        opt.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(opt);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Builder] ¡APK Release generada con éxito! Tamaño: {summary.totalSize / (1024 * 1024):F1} MB en {summary.totalTime.TotalSeconds:F1}s.");
            EditorUtility.RevealInFinder(opt.locationPathName);
        }
        else
        {
            Debug.LogError($"[Builder] Compilación fallida: {summary.result}. Errores: {summary.totalErrors}");
        }
    }
}
