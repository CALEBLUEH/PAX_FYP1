using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxFrontierAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxGeneratedFrontier.unity";
        public const string DefinitionPath = "Assets/PAX/Generation/Frontier.asset";
        public const string TerrainPath = "Assets/PAX/Generation/FrontierTerrain.asset";
        public const string UiPath = "Assets/PAX/UI/GeneratedFrontier.uxml";
        [MenuItem("PAX/Create finite generated frontier if missing")]
        public static void FromMenu()
        { PaxIslandAuthoring.RequireProjectIdentity(); if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EnsureFrontier(); }
        public static void EnsureFrontier()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            if (File.Exists(ScenePath)) { Debug.Log("PAX frontier exists; preserving all scene edits and saves."); return; }
            var definition = AssetDatabase.LoadAssetAtPath<PaxFrontierDefinition>(DefinitionPath);
            if (definition == null)
            {
                var terrain = AssetDatabase.LoadAssetAtPath<PaxGenerationDefinition>(TerrainPath);
                if (terrain == null) { terrain = ScriptableObject.CreateInstance<PaxGenerationDefinition>(); terrain.seed = 91873; AssetDatabase.CreateAsset(terrain, TerrainPath); }
                definition = ScriptableObject.CreateInstance<PaxFrontierDefinition>(); definition.terrain = terrain; AssetDatabase.CreateAsset(definition, DefinitionPath); AssetDatabase.SaveAssets();
            }
            CreateNewScene(definition, ScenePath);
        }
        public static void CreateNewScene(PaxFrontierDefinition definition, string path)
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/PAX/Scenes/", StringComparison.Ordinal) || !path.EndsWith(".unity", StringComparison.Ordinal) || path.Contains("..")) throw new ArgumentException("Choose a NEW scene under Assets/PAX/Scenes.");
            if (File.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) throw new IOException("Existing scene preserved. Choose a new filename.");
            string definitionPath = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrEmpty(definitionPath) || definition.terrain == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition.terrain))) throw new ArgumentException("Save both frontier and terrain definitions as assets first.");
            var layout = PaxFrontierLayout.Generate(definition); // All validation precedes scene mutation.
            var scene = EditorSceneManager.OpenScene(PaxResourceAuthoring.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not create frontier scene.");
            definition = AssetDatabase.LoadAssetAtPath<PaxFrontierDefinition>(definitionPath);
            var world = Object.FindFirstObjectByType<PaxWorld>(); var player = Object.FindFirstObjectByType<PaxPlayer>(); var mission = Object.FindFirstObjectByType<PaxExpedition>(); var cache = Object.FindFirstObjectByType<PaxResourceCache>(); var pause = Object.FindFirstObjectByType<PaxPause>(); var ui = Object.FindFirstObjectByType<UIDocument>();
            Object.DestroyImmediate(Object.FindFirstObjectByType<PaxGeneratedIsland>());
            Object.DestroyImmediate(Object.FindFirstObjectByType<PaxContinueReceiver>());
            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) Object.DestroyImmediate(text.gameObject);
            foreach (Transform child in mission.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in cache.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var worldData = new SerializedObject(world); var root = (Transform)worldData.FindProperty("blockRoot").objectReferenceValue; var prefab = (PaxBlock)worldData.FindProperty("blockPrefab").objectReferenceValue;
            foreach (Transform child in world.transform.Cast<Transform>().ToArray())
                if (child != root && child.childCount == 0 && child.GetComponents<Component>().Length == 1) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in root.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var materials = Enum.GetNames(typeof(PaxTerrainMaterial)).Select(n => AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/" + n + ".mat")).ToArray();
            if (materials.Any(m => m == null)) throw new InvalidOperationException("Required PAX materials missing.");
            var blocks = new List<PaxBlock>();
            foreach (var tile in layout.Tiles)
            {
                var block = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, root)).GetComponent<PaxBlock>();
                block.name = "Generated " + tile.Cell; block.Configure(tile.Cell, block.GetComponent<Renderer>(), block.GetComponent<Collider>(), tile.Mineable); block.GetComponent<Renderer>().sharedMaterial = materials[(int)tile.Material]; blocks.Add(block);
            }
            world.Configure(prefab, root, blocks); worldData.Update(); worldData.FindProperty("minimum").vector3IntValue = layout.Minimum; worldData.FindProperty("maximum").vector3IntValue = layout.Maximum; worldData.ApplyModifiedPropertiesWithoutUndo();
            var spawn = (Transform)new SerializedObject(player).FindProperty("respawn").objectReferenceValue; spawn.position = layout.Spawn; player.transform.position = spawn.position;
            var gather = layout.Supplies.Select(t => Marker(t.Material + " finite cache " + t.Cell, cache.transform, PaxGrid.Centre(t.Cell))).ToArray(); cache.Configure(gather);
            var markerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition marker.mat"); var lampMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition beacon.mat");
            var sites = new List<PaxSurveySite>();
            foreach (var structure in layout.Structures)
            {
                Marker(structure.kind + " entrance", mission.transform, (Vector3)structure.door + new Vector3(0, 0.03f, -1));
                if (structure.kind == "Ruin") continue;
                var visual = Visual(structure.kind + " survey mark", PrimitiveType.Cylinder, mission.transform, (Vector3)structure.centre + Vector3.up * 0.03f, new Vector3(0.7f, 0.025f, 0.7f), markerMat);
                var site = visual.gameObject.AddComponent<PaxSurveySite>(); site.Configure(structure.kind + " station", structure.kind == "House" ? "Timber left for a shared crossing." : "Stone left for the relay ascent.", visual); sites.Add(site);
            }
            var relayVisual = Visual("Relay survey mark", PrimitiveType.Cylinder, mission.transform, layout.Relay, new Vector3(0.7f, 0.025f, 0.7f), markerMat);
            var relaySite = relayVisual.gameObject.AddComponent<PaxSurveySite>(); relaySite.Configure("Frontier relay", "A new route joins the shelter stations.", relayVisual); sites.Add(relaySite);
            var repairs = new List<PaxRepairMarker>();
            void Repair(Vector3Int cell, string id, string materialName)
            { var visual = Visual(id + " repair " + cell, PrimitiveType.Cube, mission.transform, PaxGrid.Centre(cell), Vector3.one * 0.99f, AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/" + materialName + ".mat")); var repair = visual.gameObject.AddComponent<PaxRepairMarker>(); repair.Configure(world, visual); repair.RequireMaterial(id); repairs.Add(repair); }
            foreach (var cell in layout.Crossing) Repair(cell, "timber", "Chart amber blueprint");
            foreach (var cell in layout.Steps) Repair(cell, "stone", "Expedition blueprint");
            Visual("Relay mast", PrimitiveType.Cylinder, mission.transform, layout.Relay + new Vector3(1, 0.7f, 1), new Vector3(0.12f, 0.7f, 0.12f), markerMat);
            var lamp = Visual("Relay signal", PrimitiveType.Sphere, mission.transform, layout.Relay + new Vector3(1, 1.5f, 1), Vector3.one * 0.4f, lampMat);
            mission.Configure(player, sites.ToArray(), repairs.ToArray(), relaySite.transform, lamp);
            string guide = $"Mine {layout.Crossing.Length} Timber at the House forecourt and {layout.Steps.Length} Stone at the Cave forecourt. Read both stations, then follow the central lane north. Ctrl precise + G includes faded solids; Q/E exposes a forward side face. RMB-build amber Timber cells in order, walk across, then build cyan Stone steps and jump up. F activates at the relay; R returns to camp.";
            SetStrings(mission, new Dictionary<string,string> {
                {"expeditionTitle", "Generated frontier / Shelter relay"}, {"introduction", $"Seed {definition.terrain.seed}. A terraced island, three shelters and one broken relay approach."},
                {"surveyObjective", $"Read House and Cave stations; gather {layout.Crossing.Length} Timber + {layout.Steps.Length} Stone in their forecourts. Repair the north relay approach and survey its upper mark."},
                {"repairObjective", $"Match {layout.Crossing.Length} Timber crossing cells and {layout.Steps.Length} Stone steps. Mine mistakes to refund."},
                {"returnObjective", "Stand on the upper relay mark and press F to connect the stations."}, {"completeObjective", "Shelter relay connected. The generated frontier remains yours to explore and build. F6 resets field notes only."},
                {"initialMessage", "Fresh field notes. F5/F9/F8 preserve world, stocks and harvest history together."}, {"interruptedMessage", "Relay interrupted: restore matching repairs and activate again."},
                {"missingRepairMessage", "Restore every amber Timber cell and cyan Stone step."}, {"missingSurveyMessage", "Read both shelter stations and the relay mark first."}, {"returnMessage", "Climb to the upper relay mark, then press F."},
                {"successMessage", "Shelter relay connected. Explore freely; manual snapshots remain available."}, {"restartMessage", "Field notes restarted. Construction, stockpile and snapshots remain."} });
            SetStrings(Object.FindFirstObjectByType<PaxStockpile>(), new Dictionary<string,string> { {"instructions", guide} });
            SetStrings(pause, new Dictionary<string,string> { {"missionHelp", guide + " Manual F5 saves, F9 loads, F8 loads previous. Journal is session-only. Each new generated scene has its own slot. This standalone review opens directly; quit normally to leave."}, {"selectionScenePath", ""} });
            SetStrings(Object.FindFirstObjectByType<PaxExpeditionHud>(), new Dictionary<string,string> { {"progressFormat", "Stations {0}/{1}  ·  Matching repairs {2}/{3}"} });
            ui.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);
            string guid = AssetDatabase.AssetPathToGUID(path); if (guid.Length != 32) throw new InvalidOperationException("Generated scene GUID missing.");
            SetStrings(Object.FindFirstObjectByType<PaxPlaytestSave>(), new Dictionary<string,string> { {"saveFile", "pax-frontier-" + guid + ".xml"} });
            world.gameObject.AddComponent<PaxGeneratedFrontier>().Configure(definition, layout, spawn, Marker("Relay near bank", mission.transform, layout.NearBank), Marker("Relay far bank", mission.transform, layout.FarBank), mission, cache);
            Object.FindFirstObjectByType<PaxCamera>().SnapToTarget();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList(); scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); Debug.Log($"PAX_FRONTIER_AUTHORING_OK seed={definition.terrain.seed} tiles={blocks.Count} budget={layout.Crossing.Length}T/{layout.Steps.Length}S scene={path}");
        }
        private static void SetStrings(Object target, Dictionary<string,string> values)
        { var data = new SerializedObject(target); foreach (var pair in values) data.FindProperty(pair.Key).stringValue = pair.Value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static Transform Marker(string title, Transform parent, Vector3 position)
        { var item = new GameObject(title).transform; item.SetParent(parent); item.position = position; return item; }
        private static Renderer Visual(string title, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material)
        { var item = GameObject.CreatePrimitive(shape); item.name = title; item.layer = 2; item.transform.SetParent(parent); item.transform.position = position; item.transform.localScale = scale; Object.DestroyImmediate(item.GetComponent<Collider>()); var r = item.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; return r; }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity(); EnsureFrontier();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/PAX-Frontier-Windows/PAX.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory("Logs/PAX"); File.WriteAllText("Logs/PAX/FrontierBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Frontier Windows build failed."); Debug.Log("PAX_FRONTIER_BUILD_OK");
        }
    }
    [CustomEditor(typeof(PaxFrontierDefinition))]
    public sealed class PaxFrontierDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); EditorGUILayout.HelpBox("Edit the referenced terrain asset for seed/radius/terraces. Generation writes a NEW editable scene only; loaded scenes and saves never regenerate. Each scene gets a distinct finite save slot.", MessageType.Info);
            if (!GUILayout.Button("Create new finite frontier scene...")) return;
            PaxIslandAuthoring.RequireProjectIdentity();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = EditorUtility.SaveFilePanelInProject("New finite frontier", "PaxFrontier", "unity", "Choose a new filename.", "Assets/PAX/Scenes");
            if (string.IsNullOrEmpty(path)) return;
            try { PaxFrontierAuthoring.CreateNewScene((PaxFrontierDefinition)target, path); } catch (Exception error) { Debug.LogException(error); }
        }
    }
}
