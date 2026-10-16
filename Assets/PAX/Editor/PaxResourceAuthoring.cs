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
    public static class PaxResourceAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxResourceExpedition.unity";
        public const string RulesPath = "Assets/PAX/Generation/ResourceRules.asset";
        public const string PalettePath = "Assets/PAX/Generation/ResourcePalette.asset";
        public const string UiPath = "Assets/PAX/UI/ResourceExpedition.uxml";
        [MenuItem("PAX/Create finite resource expedition if missing")]
        public static void CreateFromMenu()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) CreateResourceExpedition(); }
        public static void CreateResourceExpedition()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            if (File.Exists(ScenePath)) { Debug.Log("Resource expedition exists; preserving edited scene and all saves."); return; }
            var scene = EditorSceneManager.OpenScene(PaxPaletteAuthoring.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot create resource scene.");
            var selection = Object.FindFirstObjectByType<PaxMaterialSelection>();
            var palette = AssetDatabase.LoadAssetAtPath<PaxMaterialPalette>(PalettePath);
            if (palette == null)
            {
                palette = Object.Instantiate(selection.Palette); palette.name = "ResourcePalette";
                var serializedPalette = new SerializedObject(palette); serializedPalette.FindProperty("paletteTitle").stringValue = "Construction / Finite stocks"; serializedPalette.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            var data = new SerializedObject(selection); data.FindProperty("palette").objectReferenceValue = palette; data.ApplyModifiedPropertiesWithoutUndo();
            var rules = AssetDatabase.LoadAssetAtPath<PaxResourceRules>(RulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<PaxResourceRules>();
                rules.Configure(palette.Entries.Select(entry => new PaxResourceRules.Entry { id = entry.id, start = 0, authoredYield = 1, limit = 64 }).ToArray());
                AssetDatabase.CreateAsset(rules, RulesPath);
            }
            if (!rules.Validate(palette, out string reason)) throw new InvalidOperationException(reason);
            var world = Object.FindFirstObjectByType<PaxWorld>();
            var builder = Object.FindFirstObjectByType<PaxBuilder>();
            var player = Object.FindFirstObjectByType<PaxPlayer>();
            var save = Object.FindFirstObjectByType<PaxPlaytestSave>();
            var document = Object.FindFirstObjectByType<UIDocument>();
            if (!File.Exists(UiPath))
            {
                string markup = File.ReadAllText(PaxPaletteAuthoring.UiPath);
                markup = FiniteUiText(markup);
                int position = markup.LastIndexOf("</ui:VisualElement>", StringComparison.Ordinal);
                markup = markup.Insert(position, "<ui:Label name=\"resource-stockpile\" text=\"Finite stockpile: mine matching blocks first.\" style=\"font-size: 14px; color: rgb(255, 222, 167); white-space: normal; margin-top: 8px;\" picking-mode=\"Ignore\" />\n    ");
                File.WriteAllText(UiPath, markup); AssetDatabase.ImportAsset(UiPath);
            }
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);
            var resourceRoot = new GameObject("Editable finite resources");
            var stockpile = resourceRoot.AddComponent<PaxStockpile>(); stockpile.Configure(world, builder, palette, rules, document);
            resourceRoot.AddComponent<PaxResourceSession>().Configure(world, player, stockpile, save);
            var worldData = new SerializedObject(world);
            var prefab = (PaxBlock)worldData.FindProperty("blockPrefab").objectReferenceValue;
            var root = (Transform)worldData.FindProperty("blockRoot").objectReferenceValue;
            var blocks = world.Blocks.ToList(); var markers = new List<Transform>();
            for (int z = -1; z <= 0; z++)
            for (int x = 0; x >= -2; x--)
            {
                var cell = new Vector3Int(x, 0, z);
                if (blocks.Any(block => block.Cell == cell)) throw new InvalidOperationException("Gather cache collides with existing authored content.");
                var tile = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, root)).GetComponent<PaxBlock>();
                tile.name = "Recoverable Stone cache " + cell; tile.Configure(cell, tile.GetComponent<Renderer>(), tile.GetComponent<Collider>());
                palette.TryResolve("stone", out var rock); tile.GetComponent<Renderer>().sharedMaterial = rock; blocks.Add(tile);
                var marker = new GameObject("Gather marker " + cell).transform; marker.SetParent(resourceRoot.transform); marker.position = PaxGrid.Centre(cell); markers.Add(marker);
            }
            world.Configure(prefab, root, blocks); resourceRoot.AddComponent<PaxResourceCache>().Configure(markers.ToArray());
            FiniteSceneText(Object.FindFirstObjectByType<PaxExpedition>(), stockpile);
            var label = new GameObject("Stone cache sign").AddComponent<TextMesh>(); label.transform.SetParent(resourceRoot.transform);
            label.text = "STONE CACHE / MINE TO BUILD"; label.fontSize = 40; label.characterSize = 0.035f; label.anchor = TextAnchor.MiddleCenter;
            label.transform.SetPositionAndRotation(new Vector3(-1, 1.4f, 0.2f), Object.FindFirstObjectByType<PaxCamera>().transform.rotation);
            string guid = AssetDatabase.AssetPathToGUID(ScenePath); if (guid.Length != 32) throw new InvalidOperationException("Resource scene GUID missing.");
            var saveData = new SerializedObject(save); saveData.FindProperty("saveFile").stringValue = "pax-resources-" + guid + ".xml"; saveData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(entry => entry.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets(); Debug.Log("PAX_RESOURCE_AUTHORING_OK six recoverable cache blocks, finite snapshot slot");
        }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity(); CreateResourceExpedition(); EnsureFiniteMessaging();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/PAX-Resources-Windows/PAX.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory("Logs/PAX");
            File.WriteAllText("Logs/PAX/ResourceBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Resource Windows build failed.");
            Debug.Log("PAX_RESOURCE_BUILD_OK");
        }
        private static string FiniteUiText(string text) => text.Replace("Session journal: resets on reload / F6. F5/F9/F8 store tiles only; loading rechecks repairs.", "Session journal resets on reload / F6. F5/F9/F8 store stockpile + tiles together; loading rechecks repairs.");
        private static bool FiniteSceneText(PaxExpedition mission, PaxStockpile pile)
        {
            var data = new SerializedObject(mission);
            if (data.FindProperty("initialMessage").stringValue == "Survey visits are session-only. F5/F9/F8 save and load tiles only.") data.FindProperty("initialMessage").stringValue = "Survey visits are session-only. F5/F9/F8 restore stockpile + tiles together.";
            if (data.FindProperty("restartMessage").stringValue == "New survey started. Existing construction and tile saves are kept.") data.FindProperty("restartMessage").stringValue = "New survey started. Stockpile, construction and snapshot files are kept.";
            bool changed = data.ApplyModifiedPropertiesWithoutUndo();
            var stockData = new SerializedObject(pile);
            if (stockData.FindProperty("instructions").stringValue == "Finite supplies: mine blocks to gather; each placement costs 1 matching unit. Mine the Stone cache near spawn to repair the beacon.") stockData.FindProperty("instructions").stringValue = "Mine the six Stone cache blocks near spawn, press 3 for Stone, then RMB-build the cyan beacon pad. Each placement costs 1 matching unit.";
            return stockData.ApplyModifiedPropertiesWithoutUndo() || changed;
        }
        public static void EnsureFiniteMessaging()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            string before = File.ReadAllText(UiPath), after = FiniteUiText(before);
            if (before != after) { File.WriteAllText(UiPath, after); AssetDatabase.ImportAsset(UiPath); }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (FiniteSceneText(Object.FindFirstObjectByType<PaxExpedition>(), Object.FindFirstObjectByType<PaxStockpile>())) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }
    }
}
