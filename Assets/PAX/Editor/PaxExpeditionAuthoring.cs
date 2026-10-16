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
    public static class PaxExpeditionAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxExpedition.unity";
        [MenuItem("PAX/Create survey expedition if missing")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateExpedition();
        }

        public static void CreateExpedition()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author the expedition in Edit Mode only.");
            if (File.Exists(ScenePath)) { Debug.Log("PAX expedition exists; preserving authored scene and saves."); return; }
            if (!File.Exists(PaxIslandAuthoring.ScenePath)) throw new FileNotFoundException("Create the generated island sandbox first.");
            var scene = EditorSceneManager.OpenScene(PaxIslandAuthoring.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not create expedition scene.");
            var world = Object.FindFirstObjectByType<PaxWorld>();
            var player = Object.FindFirstObjectByType<PaxPlayer>();
            var manifest = Object.FindFirstObjectByType<PaxGeneratedIsland>();
            var data = new SerializedObject(world);
            var root = (Transform)data.FindProperty("blockRoot").objectReferenceValue;
            var prefab = (PaxBlock)data.FindProperty("blockPrefab").objectReferenceValue;
            var blocks = new List<PaxBlock>();
            foreach (var block in world.Blocks)
            {
                var cell = block.Cell;
                if (cell.x >= -1 && cell.x <= 4 && cell.z >= -7 && cell.z <= -4 && cell.y >= 0) Object.DestroyImmediate(block.gameObject);
                else blocks.Add(block);
            }
            var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Rock.mat");
            for (int y = 0; y < 2; y++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, root);
                var block = instance.GetComponent<PaxBlock>();
                var cell = new Vector3Int(3, y, -5);
                block.name = $"Beacon support {cell}";
                block.Configure(cell, block.GetComponent<Renderer>(), block.GetComponent<Collider>(), false);
                block.GetComponent<Renderer>().sharedMaterial = rock;
                blocks.Add(block);
            }
            world.Configure(prefab, root, blocks);
            var missionRoot = new GameObject("Editable survey expedition").transform;
            var markerMaterial = Material("Expedition marker", Shader.Find("Standard"), new Color(1, 0.65f, 0.15f));
            var blueprintMaterial = Material("Expedition blueprint", Shader.Find("PAX/Preview"), new Color(0.2f, 0.85f, 1, 0.3f));
            var beaconMaterial = Material("Expedition beacon", Shader.Find("Standard"), new Color(1, 0.55f, 0.1f));
            beaconMaterial.EnableKeyword("_EMISSION");
            var sites = new List<PaxSurveySite>();
            foreach (var structure in manifest.Structures)
            {
                var indicator = Visual(structure.kind + " survey mark", PrimitiveType.Cylinder, missionRoot,
                    (Vector3)structure.centre + Vector3.up * 0.03f, new Vector3(0.8f, 0.025f, 0.8f), markerMaterial);
                string note = structure.kind == "House" ? "A dry roof and a familiar door." : structure.kind == "Cave" ? "Stone kept the storm outside." : "The old survey route still holds.";
                var site = indicator.gameObject.AddComponent<PaxSurveySite>();
                site.Configure(structure.kind, note, indicator);
                sites.Add(site);
            }
            var repairs = new List<PaxRepairMarker>();
            for (int x = 0; x <= 2; x++)
            for (int z = -6; z <= -5; z++)
            {
                var visual = Visual($"Repair platform cell {x},0,{z}", PrimitiveType.Cube, missionRoot,
                    PaxGrid.Centre(new Vector3Int(x, 0, z)), Vector3.one * 0.99f, blueprintMaterial);
                var repair = visual.gameObject.AddComponent<PaxRepairMarker>();
                repair.Configure(world, visual); repairs.Add(repair);
            }
            Visual("Beacon mast", PrimitiveType.Cylinder, missionRoot, new Vector3(3, 2.35f, -5), new Vector3(0.12f, 0.35f, 0.12f), markerMaterial);
            var lamp = Visual("Survey beacon signal", PrimitiveType.Sphere, missionRoot, new Vector3(3, 2.85f, -5), Vector3.one * 0.5f, beaconMaterial);
            var returnPoint = new GameObject("Beacon activation marker").transform;
            returnPoint.SetParent(missionRoot); returnPoint.position = new Vector3(2, 1.03f, -5);
            Visual("Beacon standing mark", PrimitiveType.Cylinder, returnPoint, returnPoint.position - Vector3.up * 0.01f, new Vector3(0.7f, 0.015f, 0.7f), markerMaterial);
            var mission = missionRoot.gameObject.AddComponent<PaxExpedition>();
            mission.Configure(player, sites.ToArray(), repairs.ToArray(), returnPoint, lamp);
            missionRoot.gameObject.AddComponent<PaxExpeditionInput>().Configure(mission);
            var document = Object.FindFirstObjectByType<UIDocument>();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/PAX/UI/Expedition.uxml");
            document.gameObject.AddComponent<PaxExpeditionHud>().Configure(document, mission, player);
            string guid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (guid.Length != 32) throw new InvalidOperationException("New expedition scene has no valid GUID.");
            var save = new SerializedObject(Object.FindFirstObjectByType<PaxPlaytestSave>());
            save.FindProperty("saveFile").stringValue = "pax-expedition-" + guid + ".xml";
            save.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(entry => entry.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"PAX_EXPEDITION_AUTHORING_OK {sites.Count} sites, {repairs.Count} repair cells, saved {ScenePath}");
        }

        private static Renderer Visual(string name, PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var visual = GameObject.CreatePrimitive(primitive);
            visual.name = name; visual.layer = 2;
            visual.transform.SetParent(parent); visual.transform.position = position; visual.transform.localScale = scale;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            var renderer = visual.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }
        private static Material Material(string name, Shader shader, Color color)
        {
            string path = "Assets/PAX/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (shader == null) throw new InvalidOperationException("Required expedition shader is missing.");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            CreateExpedition();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/PAX-Expedition-Windows/PAX.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Directory.CreateDirectory("Logs/PAX");
            File.WriteAllText("Logs/PAX/ExpeditionBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Expedition Windows build failed.");
            Debug.Log("PAX_EXPEDITION_BUILD_OK");
        }
    }
}
