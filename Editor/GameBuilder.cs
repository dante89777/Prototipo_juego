using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Compila la version jugable para Windows en la carpeta "Ejecutable" del proyecto.
/// Se puede lanzar desde el menu "Village/Compilar ejecutable (Windows)" o creando
/// el archivo centinela "_build_game.flag" en la raiz del proyecto.
/// </summary>
[InitializeOnLoad]
public static class GameBuilder
{
    const string FlagName = "_build_game.flag";
    const string OutputFolder = "Ejecutable";
    const string ExecutableName = "plantilla_2.exe";
    const string Scene = "Assets/Scenes/SampleScene.unity";

    static GameBuilder()
    {
        EditorApplication.update += WatchFlag;
    }

    static void WatchFlag()
    {
        string flag = FlagPath();
        if (!File.Exists(flag)) return;

        try
        {
            File.Delete(flag);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameBuilder] No se pudo borrar el centinela: " + e.Message);
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[GameBuilder] Sal del modo play antes de compilar.");
            return;
        }

        BuildWindows();
    }

    static string FlagPath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, FlagName);
    }

    [MenuItem("Village/Compilar ejecutable (Windows)")]
    public static void BuildWindows()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(root, OutputFolder);
        Directory.CreateDirectory(output);

        PlayerSettings.companyName = "TILE22";
        PlayerSettings.productName = "plantilla_2";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
        AssetDatabase.SaveAssets();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = Path.Combine(output, ExecutableName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[GameBuilder] Compilacion correcta: {summary.totalSize / (1024 * 1024)} MB en {summary.outputPath}");
        }
        else
        {
            Debug.LogError($"[GameBuilder] La compilacion fallo: {summary.result} ({summary.totalErrors} errores).");
        }
    }
}
