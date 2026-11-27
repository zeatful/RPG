using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>Builds a standalone Linux player of the Sandbox level into Builds/Linux.</summary>
    public static class RpgBuild
    {
        const string Scene = "Assets/Scenes/Sandbox.unity";
        public const string Output = "Builds/Linux/RPG.x86_64";

        [MenuItem("RPG/Build Linux Player")]
        public static void BuildLinux()
        {
            Build();
        }

        // Command line: Unity -batchmode -executeMethod RPG.EditorTools.RpgBuild.BuildBatch -quit
        public static void BuildBatch()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        static bool Build()
        {
            RpgArtSetup.Run();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Output,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[RPG Build] " + report.summary.result + " -> " + Output + " (" + report.summary.totalErrors + " errors)");
            return ok;
        }
    }
}
