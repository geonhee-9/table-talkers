using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TableTalkers.EditorTools
{
    /// <summary>
    /// One-click builds for the two-machine playtest. Places steam_appid.txt next to the
    /// executable so Steam initializes in standalone builds (dev builds only — remove for the
    /// real Steam depot upload, where Steam provides the App ID itself).
    /// </summary>
    public static class BuildTool
    {
        private const string ScenePath = "Assets/TableTalkers/Scenes/Boot.unity";
        private const string AppId = "480";

        [MenuItem("TableTalkers/Build/macOS (Apple Silicon)")]
        public static void BuildMacArm()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/mac/TableTalkers.app");
        }

        [MenuItem("TableTalkers/Build/Windows 64-bit")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/win/TableTalkers.exe");
        }

        private static void Build(BuildTarget target, string outPath)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[TableTalkers] Build failed: {report.summary.result}");
                return;
            }

            WriteSteamAppId(target, outPath);
            Debug.Log($"[TableTalkers] Build done → {Path.GetFullPath(outPath)} " +
                      "(steam_appid.txt placed for dev testing; target machine must have Steam running)");
            EditorUtility.RevealInFinder(outPath);
        }

        private static void WriteSteamAppId(BuildTarget target, string outPath)
        {
            if (target == BuildTarget.StandaloneOSX)
            {
                // Next to the binary inside the app bundle, and next to the .app for safety.
                string inside = Path.Combine(outPath, "Contents/MacOS/steam_appid.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(inside)!);
                File.WriteAllText(inside, AppId);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath)!, "steam_appid.txt"), AppId);
            }
            else
            {
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath)!, "steam_appid.txt"), AppId);
            }
        }
    }
}
