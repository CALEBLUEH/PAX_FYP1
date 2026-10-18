using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxEntryAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxExpeditionSelection.unity";
        public const string UiPath = "Assets/PAX/UI/ExpeditionSelection.uxml";
        public static void EnsureEntry()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Selection camera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.075f, 0.115f); camera.orthographic = true;
                camera.gameObject.AddComponent<AudioListener>();
                var ui = new GameObject("Authored expedition selection").AddComponent<UIDocument>();
                ui.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/PAX/UI/PrototypePanel.asset");
                ui.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);
                ui.gameObject.AddComponent<PaxExpeditionSelection>().Configure(ui, PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot save selection scene.");
            }
            foreach (string path in new[] { PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var data = new SerializedObject(Object.FindFirstObjectByType<PaxPause>()); var property = data.FindProperty("selectionScenePath");
                if (string.IsNullOrEmpty(property.stringValue)) { property.stringValue = ScenePath; data.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(entry => entry.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets(); Debug.Log("PAX_ENTRY_AUTHORING_OK selection + confirmed return, existing finite slots retained");
        }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity(); EnsureEntry();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath, PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath }, locationPathName = "Builds/PAX-Entry-Windows/PAX.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory("Logs/PAX"); File.WriteAllText("Logs/PAX/EntryBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Entry review build failed.");
            Debug.Log("PAX_ENTRY_BUILD_OK");
        }
    }
}
