using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class CommandLineBuild
    {
        private const string GuardKey = "JiggleRunner.Build.InProgress";
        private const string OutputPath = "C:/Projects/MY/Jiggle Physics/Builds/JiggleRunner/JiggleRunner.exe";

        public static void Build()
        {
            if (EditorPrefs.GetBool(GuardKey, false))
            {
                Debug.Log("[CommandLineBuild] build already in progress, skipping.");
                return;
            }

            EditorPrefs.SetBool(GuardKey, true);
            try
            {
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/Scenes/ParkourCity.unity",
                        "Assets/Scenes/SkyTemple.unity",
                        "Assets/Scenes/SampleScene.unity"
                    },
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log("[CommandLineBuild] result=" + report.summary.result + " errors=" + report.summary.totalErrors + " bytes=" + report.summary.totalSize);
            }
            finally
            {
                EditorPrefs.SetBool(GuardKey, false);
            }
        }
    }
}
