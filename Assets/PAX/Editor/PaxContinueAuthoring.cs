using System;
using System.Collections.Generic;
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
    public static class PaxContinueAuthoring
    {
        public const string CatalogPath = "Assets/PAX/Generation/ContinueCatalog.asset";
        public const string PanelPath = "Assets/PAX/UI/ContinueSelectionPanel.asset";
        [Serializable] private sealed class Definition { public PaxContinueSlot[] slots; }
        public static void EnsureContinue()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            var definitions = new List<PaxContinueSlot>();
            foreach (string path in new[] { PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath, PaxEmberAuthoring.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var world = Object.FindFirstObjectByType<PaxWorld>(); var pile = Object.FindFirstObjectByType<PaxStockpile>(); var save = Object.FindFirstObjectByType<PaxPlaytestSave>(); var pause = Object.FindFirstObjectByType<PaxPause>();
                var prefab = (PaxBlock)new SerializedObject(world).FindProperty("blockPrefab").objectReferenceValue;
                if (!pile.Rules.Validate(pile.Palette, out string reason)) throw new InvalidOperationException(reason);
                definitions.Add(new PaxContinueSlot { scenePath = path, folder = save.SaveFolder, file = save.SaveFile, minimum = world.Minimum, maximum = world.Maximum, defaultMaterial = prefab.SurfaceMaterial, rules = pile.Rules, palette = pile.Palette,
                    tiles = world.Blocks.OrderBy(block => block.Cell.x).ThenBy(block => block.Cell.y).ThenBy(block => block.Cell.z).Select(block => new PaxContinueSlot.Tile { cell = block.Cell, material = block.SurfaceMaterial, mineable = block.Mineable }).ToArray() });
                var receiver = Object.FindFirstObjectByType<PaxContinueReceiver>();
                if (receiver == null)
                { receiver = pause.gameObject.AddComponent<PaxContinueReceiver>(); receiver.Configure(save, pile, world, pause); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            var catalog = AssetDatabase.LoadAssetAtPath<PaxContinueCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<PaxContinueCatalog>(); catalog.Configure(definitions.ToArray()); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            else
            {
                string before = JsonUtility.ToJson(new Definition { slots = catalog.Slots.ToArray() }), after = JsonUtility.ToJson(new Definition { slots = definitions.ToArray() });
                if (before != after) { catalog.Configure(definitions.ToArray()); EditorUtility.SetDirty(catalog); }
            }
            AssetDatabase.SaveAssets();
            var selectionScene = EditorSceneManager.OpenScene(PaxEntryAuthoring.ScenePath, OpenSceneMode.Single);
            // Opening a scene may unload unused assets; reacquire before assigning a scene reference.
            catalog = AssetDatabase.LoadAssetAtPath<PaxContinueCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Continue catalog did not load after scene switch.");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null) { panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/PAX/UI/PrototypePanel.asset")); panel.name = "ContinueSelectionPanel"; panel.scaleMode = PanelScaleMode.ConstantPixelSize; AssetDatabase.CreateAsset(panel, PanelPath); }
            var document = Object.FindFirstObjectByType<UIDocument>(); bool panelChanged = document.panelSettings != panel; if (panelChanged) document.panelSettings = panel;
            var data = new SerializedObject(Object.FindFirstObjectByType<PaxExpeditionSelection>()); var reference = data.FindProperty("continueCatalog");
            if (reference.objectReferenceValue != catalog) reference.objectReferenceValue = catalog;
            var message = data.FindProperty("readyMessage"); if (message.stringValue == "All three expeditions are available. Choose a fresh expedition.") message.stringValue = "1 / 2 / 3 start fresh. 4 / 5 / 6 Continue when a compatible snapshot is available.";
            if (data.ApplyModifiedPropertiesWithoutUndo() || panelChanged) { EditorSceneManager.MarkSceneDirty(selectionScene); EditorSceneManager.SaveScene(selectionScene); }
            AssetDatabase.SaveAssets(); Debug.Log("PAX_CONTINUE_AUTHORING_OK three live/context validators, explicit slots, three receivers");
        }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity(); EnsureContinue();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { PaxEntryAuthoring.ScenePath, PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath, PaxEmberAuthoring.ScenePath }, locationPathName = "Builds/PAX-Continue-Windows/PAX.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory("Logs/PAX"); File.WriteAllText("Logs/PAX/ContinueBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Continue review build failed."); Debug.Log("PAX_CONTINUE_BUILD_OK");
        }
    }
}
