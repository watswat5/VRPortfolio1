using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CommandLineAndroidBuild
{
    public static void BuildApk()
    {
        string outputPath = GetArgument("-buildPath");
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Pass -buildPath followed by the APK output path.");

        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes are configured for the build.");

        EditorUserBuildSettings.buildAppBundle = false;
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                $"Android build {report.summary.result} with {report.summary.totalErrors} error(s).");
    }

    static string GetArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
            if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                return arguments[i + 1];
        return null;
    }
}
