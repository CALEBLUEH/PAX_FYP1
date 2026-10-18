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
    public static class PaxEmberAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxEmberReach.unity";
        public const string UiPath = "Assets/PAX/UI/EmberReach.uxml";
        public const string Guide = "5 Timber: cross north (2 cells), then east from the cairn (3 cells). Ctrl precise + Q/E to expose the next forward side face; RMB it, then walk onto the new tile. Home resets the view for 2 Stone steps. R rescues a fall.";
        public static void EnsureEmber()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            if (!File.Exists(ScenePath)) Create();
            WireSelection(); AssetDatabase.SaveAssets(); Debug.Log("PAX_EMBER_AUTHORING_OK north/east ridge, 5 Timber + 2 Stone, three lamps");
        }
        private static void Create()
        {
            var scene = EditorSceneManager.OpenScene(PaxChartAuthoring.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot create Ember Reach scene.");
            var world = Object.FindFirstObjectByType<PaxWorld>(); var player = Object.FindFirstObjectByType<PaxPlayer>(); var rig = Object.FindFirstObjectByType<PaxCamera>();
            var mission = Object.FindFirstObjectByType<PaxExpedition>(); var cache = Object.FindFirstObjectByType<PaxResourceCache>(); var ui = Object.FindFirstObjectByType<UIDocument>();
            var worldData = new SerializedObject(world); var root = (Transform)worldData.FindProperty("blockRoot").objectReferenceValue; var prefab = (PaxBlock)worldData.FindProperty("blockPrefab").objectReferenceValue;
            foreach (Transform child in root.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in mission.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var nodes = new SerializedObject(cache).FindProperty("gatherMarkers"); for (int i = 0; i < nodes.arraySize; i++) Object.DestroyImmediate(((Transform)nodes.GetArrayElementAtIndex(i).objectReferenceValue).gameObject);
            var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Grass.mat"); var earth = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Earth.mat"); var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Rock.mat"); var timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Timber.mat");
            var blocks = new Dictionary<Vector3Int, PaxBlock>();
            void Tile(int x, int y, int z, Material material, bool mineable = true)
            {
                var cell = new Vector3Int(x, y, z); if (blocks.ContainsKey(cell)) return;
                var tile = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, root)).GetComponent<PaxBlock>(); tile.name = "Authored " + cell;
                tile.Configure(cell, tile.GetComponent<Renderer>(), tile.GetComponent<Collider>(), mineable); tile.GetComponent<Renderer>().sharedMaterial = material; blocks.Add(cell, tile);
            }
            for (int x = -3; x <= 10; x++) for (int z = -7; z <= 5; z++)
            {
                Tile(x, -4, z, rock, false);
                bool island = x <= 3 && z <= -2 || x >= -2 && x <= 2 && z >= 1 || x >= 6 && z >= 1;
                if (island) { Tile(x, -3, z, earth, false); Tile(x, -2, z, earth); Tile(x, -1, z, grass); }
            }
            for (int x = 8; x <= 10; x++) for (int z = 1; z <= 5; z++) { Tile(x, 0, z, rock, false); Tile(x, 1, z, rock, false); }
            Tile(7, 0, 3, rock, false);
            var gather = new List<Transform>();
            foreach (int x in new[] { 0, -1, 1, -2, 2 }) { Tile(x, 0, -6, timber); gather.Add(Marker("Timber cache " + x, cache.transform, new Vector3(x, 0.5f, -6))); }
            foreach (int x in new[] { -1, -2 }) { Tile(x, 0, -3, rock); gather.Add(Marker("Stone cache " + x, cache.transform, new Vector3(x, 0.5f, -3))); }
            cache.Configure(gather.ToArray()); world.Configure(prefab, root, blocks.Values.ToList());
            worldData.Update(); worldData.FindProperty("minimum").vector3IntValue = new Vector3Int(-3, -4, -7); worldData.FindProperty("maximum").vector3IntValue = new Vector3Int(10, 8, 5); worldData.ApplyModifiedPropertiesWithoutUndo();
            var spawn = (Transform)new SerializedObject(player).FindProperty("respawn").objectReferenceValue; spawn.position = new Vector3(0, 0.03f, -5); player.transform.position = spawn.position; rig.SnapToTarget();
            var amber = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Chart amber blueprint.mat"); var cyan = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition blueprint.mat"); var mark = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition marker.mat"); var lampMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition beacon.mat");
            PaxRepairMarker Repair(Vector3Int cell, string id, Material material)
            { var visual = Visual(id + " repair " + cell, PrimitiveType.Cube, mission.transform, PaxGrid.Centre(cell), Vector3.one * 0.99f, material); var repair = visual.gameObject.AddComponent<PaxRepairMarker>(); repair.Configure(world, visual); repair.RequireMaterial(id); return repair; }
            var approach = new[] { new Vector3Int(0, -1, -1), new Vector3Int(0, -1, 0), new Vector3Int(3, -1, 3), new Vector3Int(4, -1, 3), new Vector3Int(5, -1, 3) }.Select(cell => Repair(cell, "timber", amber)).ToArray();
            var steps = new[] { new Vector3Int(7, 1, 3), new Vector3Int(6, 0, 3) }.Select(cell => Repair(cell, "stone", cyan)).ToArray();
            PaxSurveySite Site(string title, string note, Vector3 position)
            { var visual = Visual(title, PrimitiveType.Cylinder, mission.transform, position, new Vector3(0.7f, 0.025f, 0.7f), mark); var site = visual.gameObject.AddComponent<PaxSurveySite>(); site.Configure(title, note, visual); return site; }
            var ledger = Site("Wind ledger", "Timber carries the path; Stone takes the height.", new Vector3(1, 0.03f, -4));
            var cairn = Site("Leeward cairn", "The lamps were left for strangers, not owners.", new Vector3(2, 0.03f, 3));
            var bowl = Site("Keeper's bowl", "Take no flame away. Leave a way for whoever comes after.", new Vector3(9, 2.08f, 3));
            Renderer Lamp(string name, Vector3 feet)
            { Visual(name + " post", PrimitiveType.Cylinder, mission.transform, feet + Vector3.up * 0.6f, new Vector3(0.13f, 0.6f, 0.13f), timber); return Visual(name + " lamp", PrimitiveType.Sphere, mission.transform, feet + Vector3.up * 1.3f, Vector3.one * 0.35f, lampMaterial); }
            var lamps = new[] { Lamp("Camp lamp", new Vector3(1, 0, -3)), Lamp("Cairn lamp", new Vector3(1, 0, 4)), Lamp("Keeper lamp", new Vector3(10, 2, 4)) };
            Visual("Keeper's bowl display", PrimitiveType.Cylinder, mission.transform, new Vector3(9, 2.06f, 3), new Vector3(0.8f, 0.06f, 0.8f), AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Lamplighter parchment.mat"));
            mission.Configure(player, new[] { ledger, cairn, bowl }, approach.Concat(steps).ToArray(), bowl.transform, lamps[2]);
            mission.gameObject.AddComponent<PaxCompletionLamps>().Configure(mission, new[] { lamps[0], lamps[1] });
            var data = new SerializedObject(mission);
            var text = new Dictionary<string, string> {
                {"expeditionTitle", "Ember Reach / Three lamps"}, {"introduction", "The chart was right: three lamps stand on a broken ridge. Leave a safe way for the next walker."},
                {"surveyObjective", "Read the Wind ledger, cross north to the Leeward cairn, then cross east and climb to the Keeper's bowl."}, {"repairObjective", "Match 5 Timber crossing cells and 2 Stone steps. Mine wrong materials to refund them."},
                {"returnObjective", "Stand at the Keeper's bowl on the upper ridge and press F to rekindle the three lamps."}, {"completeObjective", "Three lamps burn steady. A route kept for strangers is a home offered forward. Keep exploring; F6 resets field notes only."},
                {"initialMessage", "Field notes are session-only. F5/F9/F8 store stocks + tiles together."}, {"interruptedMessage", "The ridge route is broken or uses the wrong material. Repair it and rekindle at the bowl."},
                {"missingSurveyMessage", "Read the ledger, cairn and bowl notes first."}, {"missingRepairMessage", "Restore both Timber crossings and the two matching Stone steps."}, {"alreadyActiveMessage", "The three lamps are steady. The ridge is open."},
                {"returnMessage", "Reach the Keeper's bowl on foot, then press F."}, {"successMessage", "The keepers left a way home for strangers. Ember Reach is open."}, {"restartMessage", "Field notes reset; route, finite stocks and saved files are kept."} };
            foreach (var pair in text) data.FindProperty(pair.Key).stringValue = pair.Value; data.ApplyModifiedPropertiesWithoutUndo();
            if (!File.Exists(UiPath))
            {
                string markup = File.ReadAllText(PaxChartAuthoring.UiPath).Replace("F Recover chart   ·   F6 Restart discoveries", "F Rekindle lamps   ·   F6 Restart field notes")
                    .Replace("Quiet Shoal / Survey beacon", "Ember Reach / Three lamps").Replace("Read the shelter marks. Restore the island signal.", "Follow the lamps across the broken ridge.")
                    .Replace("Survey the three shelters.", "Read the three ridge field notes.").Replace("House / Cave / Ruin", "Ledger / Cairn / Bowl").Replace("5 Timber approach / 3 Stone lookout", "5 Timber crossings / 2 Stone steps").Replace("Survey 0/3 · Solid platform cells 0/6", "Field notes 0/3 · Matching repairs 0/7");
                File.WriteAllText(UiPath, markup); AssetDatabase.ImportAsset(UiPath);
            }
            ui.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);
            var route = Object.FindFirstObjectByType<PaxChartRoute>(); route.Configure(ui, approach, steps, new[] { ledger.transform, Marker("North bank", mission.transform, new Vector3(0, 0.03f, -2)), cairn.transform, Marker("East crossing takeoff", mission.transform, new Vector3(5, 0.03f, 3)), Marker("First Stone landing", mission.transform, new Vector3(6, 1.03f, 3)), Marker("Upper ridge landing", mission.transform, new Vector3(8, 2.03f, 3)), bowl.transform });
            var routeData = new SerializedObject(route); routeData.FindProperty("guide").stringValue = Guide; routeData.ApplyModifiedPropertiesWithoutUndo();
            var pile = new SerializedObject(Object.FindFirstObjectByType<PaxStockpile>()); pile.FindProperty("instructions").stringValue = "Gather 5 Timber from the south row and 2 Stone to the northwest. Cross north, turn east at the cairn, then use Stone for the keeper's steps. Mine mistakes to refund; R returns to camp."; pile.ApplyModifiedPropertiesWithoutUndo();
            var pause = new SerializedObject(Object.FindFirstObjectByType<PaxPause>()); pause.FindProperty("missionHelp").stringValue = "Ember Reach: 5 Timber repairs form two crossings (north 2, east 3). Use 2 Stone steps to climb the keeper's ridge. Read the three field notes, then F at the bowl rekindles the lamps. Wrong material? Mine and replace it."; pause.ApplyModifiedPropertiesWithoutUndo();
            var storage = new SerializedObject(Object.FindFirstObjectByType<PaxPlaytestSave>()); storage.FindProperty("saveFile").stringValue = "pax-ember-" + AssetDatabase.AssetPathToGUID(ScenePath) + ".xml"; storage.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Debug.Log("PAX_EMBER_SCENE_OK " + blocks.Count + " authored tiles");
        }
        private static Transform Marker(string name, Transform parent, Vector3 position) { var item = new GameObject(name).transform; item.SetParent(parent); item.position = position; return item; }
        private static Renderer Visual(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material)
        { var item = GameObject.CreatePrimitive(shape); item.name = name; item.layer = 2; item.transform.SetParent(parent); item.transform.position = position; item.transform.localScale = scale; Object.DestroyImmediate(item.GetComponent<Collider>()); var renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; return renderer; }
        private static void WireSelection()
        {
            var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(item => item.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            var scene = EditorSceneManager.OpenScene(PaxEntryAuthoring.ScenePath, OpenSceneMode.Single); var data = new SerializedObject(Object.FindFirstObjectByType<PaxExpeditionSelection>());
            if (string.IsNullOrEmpty(data.FindProperty("emberScenePath").stringValue)) data.FindProperty("emberScenePath").stringValue = ScenePath;
            if (data.FindProperty("readyMessage").stringValue == "Both expeditions are available. Choose a fresh expedition.") data.FindProperty("readyMessage").stringValue = "All three expeditions are available. Choose a fresh expedition.";
            if (data.ApplyModifiedPropertiesWithoutUndo()) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity(); EnsureEmber();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { PaxEntryAuthoring.ScenePath, PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath, ScenePath }, locationPathName = "Builds/PAX-Ember-Windows/PAX.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory("Logs/PAX"); File.WriteAllText("Logs/PAX/EmberBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Ember review build failed."); Debug.Log("PAX_EMBER_BUILD_OK");
        }
    }
}
