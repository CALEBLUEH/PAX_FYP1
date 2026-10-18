using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxPauseAuthoring
    {
        public const string HelpPath = "Assets/PAX/UI/PauseHelp.uxml";
        public static void EnsurePause()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            WireScene(PaxResourceAuthoring.ScenePath, PaxResourceAuthoring.UiPath,
                "Quiet Shoal: gather six Stone cache blocks near camp, press 3 for Stone and restore the six cyan pad cells. Survey the three shelters, reach the beacon on foot, then F activates it.");
            WireScene(PaxChartAuthoring.ScenePath, PaxChartAuthoring.UiPath,
                "Lowtide: gather 5 Timber + 3 Stone. Amber crossing cells require Timber; cyan stairs require Stone. Cross to the mooring, climb to the lookout and read all three marks. F recovers the chart. Wrong material? Mine it to refund and replace it.");
            AssetDatabase.SaveAssets();
            Debug.Log("PAX_PAUSE_AUTHORING_OK two finite scenes, shared authored controls, existing slots retained");
        }
        private static void WireScene(string path, string uiPath, string help)
        {
            string before = File.ReadAllText(uiPath);
            if (!before.Contains("template=\"PauseHelp\""))
            {
                string after = before.Replace("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">", "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n    <ui:Template name=\"PauseHelp\" src=\"project://database/" + HelpPath + "\" />")
                    .Replace("WASD Move   ·   Space Hold / tap jump   ·   R Respawn", "WASD Move   ·   Space Hold / tap jump   ·   R Respawn   ·   Esc Pause / Help")
                    .Replace("</ui:UXML>", "    <ui:Instance template=\"PauseHelp\" picking-mode=\"Ignore\" style=\"position: absolute; left: 0; right: 0; top: 0; bottom: 0;\" />\n</ui:UXML>");
                File.WriteAllText(uiPath, after); AssetDatabase.ImportAsset(uiPath);
            }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var ui = Object.FindFirstObjectByType<UIDocument>();
            var player = Object.FindFirstObjectByType<PaxPlayer>(); var builder = Object.FindFirstObjectByType<PaxBuilder>();
            var pause = Object.FindFirstObjectByType<PaxPause>(); bool changed = false;
            if (pause == null) { pause = ui.gameObject.AddComponent<PaxPause>(); pause.Configure(ui, player, builder, help); changed = true; }
            foreach (var target in new MonoBehaviour[] { player, builder, Object.FindFirstObjectByType<PaxCamera>(), Object.FindFirstObjectByType<PaxInput>(), Object.FindFirstObjectByType<PaxExpedition>(), Object.FindFirstObjectByType<PaxExpeditionInput>(), Object.FindFirstObjectByType<PaxMaterialSelection>(), Object.FindFirstObjectByType<PaxPlaytestSave>() })
            {
                var data = new SerializedObject(target); var property = data.FindProperty("pause");
                if (property.objectReferenceValue != pause) { property.objectReferenceValue = pause; data.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
            }
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity(); EnsurePause();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath }, locationPathName = "Builds/PAX-Pause-Windows/PAX.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory("Logs/PAX"); File.WriteAllText("Logs/PAX/PauseBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Pause review build failed.");
            Debug.Log("PAX_PAUSE_BUILD_OK");
        }
    }
}
