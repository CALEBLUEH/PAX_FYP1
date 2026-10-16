using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxIslandAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxGeneratedIsland.unity";
        public const string DefinitionPath = "Assets/PAX/Generation/TripIsland.asset";

        // Batch callers must supply the intended root independently of Unity's selected project.
        public static void ValidateProjectIdentity(string expectedProject)
        {
            string expectedAssets = Path.GetFullPath(Path.Combine(expectedProject, "Assets")).TrimEnd('/', '\\');
            string actualAssets = Path.GetFullPath(Application.dataPath).TrimEnd('/', '\\');
            if (!string.Equals(expectedAssets, actualAssets, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"PAX authoring project mismatch. Expected {expectedAssets}; actual {actualAssets}");
            Debug.Log("PAX_AUTHORING_PROJECT_CONFIRMED " + actualAssets);
        }

        private static void RequireProjectIdentity()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-paxAuthoringProject");
            if (index >= 0 && index + 1 < args.Length) ValidateProjectIdentity(args[index + 1]);
            else if (Application.isBatchMode) throw new InvalidOperationException("Batch authoring requires -paxAuthoringProject with the expected absolute project root.");
            else ValidateProjectIdentity(Directory.GetCurrentDirectory());
        }

        [MenuItem("PAX/Create generated island if missing")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateTripIsland();
        }

        public static void CreateTripIsland()
        {
            RequireProjectIdentity();
            if (File.Exists(ScenePath)) { Debug.Log("PAX generated scene exists; preserving all authored edits."); return; }
            if (!AssetDatabase.IsValidFolder("Assets/PAX/Generation")) AssetDatabase.CreateFolder("Assets/PAX", "Generation");
            var definition = AssetDatabase.LoadAssetAtPath<PaxGenerationDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PaxGenerationDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            CreateNewScene(definition, ScenePath);
        }

        public static void CreateNewScene(PaxGenerationDefinition definition, string path)
        {
            RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Generate in Edit Mode only.");
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/PAX/Scenes/", StringComparison.Ordinal) || !path.EndsWith(".unity", StringComparison.Ordinal) || path.Contains(".."))
                throw new ArgumentException("Choose a new scene inside Assets/PAX/Scenes.");
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                throw new IOException("Existing scene preserved. Choose a new filename to generate another world.");
            if (definition == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition)))
                throw new ArgumentException("Save the generation definition as an asset before creating a scene.");
            var layout = PaxIslandLayout.Generate(definition); // Validate before changing scenes.
            var scene = EditorSceneManager.OpenScene(PaxPrototypeAuthoring.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not create new scene.");
            // Clone the wired gameplay rig; all modifications now belong to the NEW scene.
            var world = Object.FindFirstObjectByType<PaxWorld>();
            var serializedWorld = new SerializedObject(world);
            var root = (Transform)serializedWorld.FindProperty("blockRoot").objectReferenceValue;
            var prefab = (PaxBlock)serializedWorld.FindProperty("blockPrefab").objectReferenceValue;
            foreach (Transform child in root.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) Object.DestroyImmediate(text.gameObject);
            var materials = Enum.GetNames(typeof(PaxTerrainMaterial)).Select(name => AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/" + name + ".mat")).ToArray();
            if (materials.Any(material => material == null)) throw new InvalidOperationException("Required PAX material missing.");
            var blocks = new System.Collections.Generic.List<PaxBlock>();
            foreach (var tile in layout.Tiles)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, root);
                var block = instance.GetComponent<PaxBlock>();
                block.name = $"Tile {tile.Cell.x},{tile.Cell.y},{tile.Cell.z}";
                block.Configure(tile.Cell, block.GetComponent<Renderer>(), block.GetComponent<Collider>(), tile.Mineable);
                block.GetComponent<Renderer>().sharedMaterial = materials[(int)tile.Material];
                blocks.Add(block);
            }
            world.Configure(prefab, root, blocks);
            serializedWorld.Update();
            serializedWorld.FindProperty("minimum").vector3IntValue = layout.Minimum;
            serializedWorld.FindProperty("maximum").vector3IntValue = layout.Maximum;
            serializedWorld.ApplyModifiedPropertiesWithoutUndo();
            var player = Object.FindFirstObjectByType<PaxPlayer>();
            var spawn = (Transform)new SerializedObject(player).FindProperty("respawn").objectReferenceValue;
            spawn.position = layout.Spawn; player.transform.position = spawn.position;
            world.gameObject.AddComponent<PaxGeneratedIsland>().Configure(definition, layout, spawn);
            foreach (var structure in layout.Structures)
            {
                var marker = new GameObject(structure.kind + " entrance marker").transform;
                marker.SetParent(world.transform); marker.position = (Vector3)structure.door + new Vector3(0, 0.03f, -1);
            }
            var save = new SerializedObject(Object.FindFirstObjectByType<PaxPlaytestSave>());
            save.FindProperty("saveFile").stringValue = "pax-island-" + AssetDatabase.AssetPathToGUID(path) + ".xml";
            save.ApplyModifiedPropertiesWithoutUndo();
            Object.FindFirstObjectByType<PaxCamera>().SnapToTarget();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(entry => entry.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"PAX_ISLAND_AUTHORING_OK seed={definition.seed} tiles={blocks.Count} scene={path}");
        }

        public static void BuildWindows()
        {
            RequireProjectIdentity();
            CreateTripIsland();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/PAX-Generated-Windows/PAX.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Directory.CreateDirectory("Logs/PAX");
            File.WriteAllText("Logs/PAX/GenerationBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Generated island Windows build failed.");
            Debug.Log("PAX_GENERATION_BUILD_OK");
        }
    }

    [CustomEditor(typeof(PaxGenerationDefinition))]
    public sealed class PaxGenerationDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Settings apply only when creating a NEW saved scene. Existing scenes and playtest saves are preserved.", MessageType.Info);
            if (!GUILayout.Button("Create new editable island scene...")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = EditorUtility.SaveFilePanelInProject("New PAX island", "PaxIsland", "unity", "Choose a new filename.", "Assets/PAX/Scenes");
            if (string.IsNullOrEmpty(path)) return;
            try { PaxIslandAuthoring.CreateNewScene((PaxGenerationDefinition)target, path); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
