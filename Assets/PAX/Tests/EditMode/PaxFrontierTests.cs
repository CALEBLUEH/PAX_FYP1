using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pax.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxFrontierTests
    {
        private PaxFrontierDefinition definition; private PaxGenerationDefinition terrain;
        [SetUp] public void Setup() { terrain = ScriptableObject.CreateInstance<PaxGenerationDefinition>(); definition = ScriptableObject.CreateInstance<PaxFrontierDefinition>(); definition.terrain = terrain; }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(definition); Object.DestroyImmediate(terrain); }
        private static string Snapshot(PaxFrontierLayout l) => string.Join(";", l.Tiles.Select(t => $"{t.Cell}:{t.Material}:{t.Mineable}"));
        [Test] public void SameDefinitionIsExactSortedAndIndependentOfUnityRandom()
        {
            var state = UnityEngine.Random.state; var first = PaxFrontierLayout.Generate(definition);
            Assert.AreEqual(Snapshot(first), Snapshot(PaxFrontierLayout.Generate(definition))); Assert.AreEqual(state, UnityEngine.Random.state);
            terrain.seed++; var varied = PaxFrontierLayout.Generate(definition); Assert.AreNotEqual(Snapshot(first), Snapshot(varied));
            Assert.IsFalse(first.Supplies.Select(t => t.Cell).SequenceEqual(varied.Supplies.Select(t => t.Cell)) && first.Structures.Select(s => s.centre).SequenceEqual(varied.Structures.Select(s => s.centre)), "Seed changes structures or supply positions as well as terrain.");
            CollectionAssert.AreEqual(first.Tiles.OrderBy(t => t.Cell.x).ThenBy(t => t.Cell.y).ThenBy(t => t.Cell.z).Select(t => t.Cell), first.Tiles.Select(t => t.Cell));
        }
        private static HashSet<Vector3Int> Walkable(Dictionary<Vector3Int, PaxGeneratedTile> cells, Vector3 start, bool jump)
        {
            var visited = new HashSet<Vector3Int>(); var queued = new HashSet<Vector3Int>(); var queue = new Queue<Vector3Int>();
            queue.Enqueue(new Vector3Int(Mathf.RoundToInt(start.x), Mathf.FloorToInt(start.y), Mathf.RoundToInt(start.z)));
            while (queue.Count > 0)
            {
                var c = queue.Dequeue(); if (!queued.Add(c) || !cells.ContainsKey(c + Vector3Int.down) || cells.ContainsKey(c) || cells.ContainsKey(c + Vector3Int.up)) continue;
                visited.Add(c);
                foreach (var direction in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back })
                    for (int dy = jump ? -1 : 0; dy <= (jump ? 1 : 0); dy++) queue.Enqueue(c + direction + Vector3Int.up * dy);
            }
            return visited;
        }
        [TestCase(12,0,2,1,2)] [TestCase(12,2,4,2,5)] [TestCase(16,2,3,2,3)] [TestCase(16,0,4,1,4)]
        public void VariedSeedsAndBoundsGuaranteeCachesProtectedRouteAndExactRepairBudget(int radius, int height, int length, int stairs, int patch)
        {
            terrain.radius = radius; terrain.terraceHeight = height; terrain.terrainPatchSize = patch; definition.crossingLength = length; definition.lookoutHeight = stairs;
            foreach (int seed in new[] { 0, -1, int.MinValue, int.MaxValue, 91873, 16427, 421, 777 })
            {
                terrain.seed = seed; var l = PaxFrontierLayout.Generate(definition); var cells = l.Tiles.ToDictionary(t => t.Cell); string reason = $"seed={seed} radius={radius} length={length} height={stairs}";
                Assert.AreEqual(l.Tiles.Count, cells.Count, reason); Assert.Less(l.Tiles.Count, 15000);
                foreach (var t in l.Tiles) { Assert.IsTrue(t.Cell.x >= l.Minimum.x && t.Cell.x <= l.Maximum.x && t.Cell.y >= l.Minimum.y && t.Cell.y <= l.Maximum.y && t.Cell.z >= l.Minimum.z && t.Cell.z <= l.Maximum.z, reason); if (t.Cell.y == -3) Assert.IsFalse(t.Mineable); }
                Assert.AreEqual(length, l.Supplies.Count(t => t.Material == PaxTerrainMaterial.Timber)); Assert.AreEqual(stairs, l.Supplies.Count(t => t.Material == PaxTerrainMaterial.Rock));
                var before = Walkable(cells, l.Spawn, false);
                foreach (var s in l.Structures) { Assert.IsTrue(before.Contains(s.door), reason + " entrance " + s.kind); Assert.IsTrue(before.Contains(s.centre), reason + " interior " + s.kind); }
                foreach (var supply in l.Supplies)
                { Assert.IsTrue(supply.Mineable); Assert.IsTrue(before.Any(c => c.y == 0 && new Vector2(c.x - supply.Cell.x, c.z - supply.Cell.z).sqrMagnitude <= 9), reason + " reachable cache " + supply.Cell); Assert.IsFalse(cells[supply.Cell + Vector3Int.down].Mineable); }
                Assert.IsTrue(before.Contains(new Vector3Int(Mathf.RoundToInt(l.NearBank.x), 0, Mathf.RoundToInt(l.NearBank.z))), reason + " near bank");
                foreach (var c in l.Crossing.Concat(l.Steps)) Assert.IsFalse(cells.ContainsKey(c), reason + " empty blueprint");
                foreach (var c in l.Crossing) cells.Add(c, new PaxGeneratedTile(c, PaxTerrainMaterial.Timber));
                foreach (var c in l.Steps) cells.Add(c, new PaxGeneratedTile(c, PaxTerrainMaterial.Rock));
                var after = Walkable(cells, l.Spawn, true);
                Assert.IsTrue(after.Contains(new Vector3Int(Mathf.RoundToInt(l.Relay.x), stairs, Mathf.RoundToInt(l.Relay.z))), reason + " completed relay");
            }
        }
        [Test] public void InvalidKnobsAndMissingSheltersFailBeforeGeneration()
        {
            definition.crossingLength = 5; Assert.Throws<ArgumentException>(() => PaxFrontierLayout.Generate(definition)); definition.crossingLength = 3;
            definition.lookoutHeight = 0; Assert.Throws<ArgumentException>(() => PaxFrontierLayout.Generate(definition)); definition.lookoutHeight = 2;
            terrain.house = false; Assert.Throws<ArgumentException>(() => PaxFrontierLayout.Generate(definition)); terrain.house = true;
            terrain.radius = 11; Assert.Throws<ArgumentException>(() => PaxFrontierLayout.Generate(definition));
        }
        [Test] public void DefaultSceneMatchesGeneratedTilesBudgetMarkersAndExplicitReferences()
        {
            var scene = EditorSceneManager.OpenScene(PaxFrontierAuthoring.ScenePath, OpenSceneMode.Single); var f = Object.FindFirstObjectByType<PaxGeneratedFrontier>(); var world = Object.FindFirstObjectByType<PaxWorld>(); var expected = PaxFrontierLayout.Generate(f.Definition);
            Assert.AreEqual(expected.Tiles.Count, f.AuthoredTileCount); Assert.AreEqual(expected.Tiles.Count, world.Blocks.Count); var actual = world.Blocks.ToDictionary(b => b.Cell);
            foreach (var t in expected.Tiles) { Assert.AreEqual(t.Material.ToString(), actual[t.Cell].SurfaceMaterial.name); Assert.AreEqual(t.Mineable, actual[t.Cell].Mineable); Assert.NotNull(actual[t.Cell].Solid); }
            CollectionAssert.AreEquivalent(expected.Supplies.Select(t => t.Cell), f.Cache.Cells); CollectionAssert.AreEquivalent(expected.Crossing.Concat(expected.Steps), f.Mission.Repairs.Select(r => r.Cell));
            Assert.AreEqual(expected.Spawn, f.SpawnMarker.position); Assert.AreEqual(expected.NearBank, f.NearBank.position); Assert.AreEqual(expected.Relay, f.Mission.BeaconReturn.position);
            Assert.AreEqual("", Object.FindFirstObjectByType<PaxPause>().SelectionScenePath); Assert.IsNull(Object.FindFirstObjectByType<PaxContinueReceiver>());
            foreach (var component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                Assert.NotNull(component, "Missing script"); if (component.GetType().Namespace != "Pax") continue;
                var p = new SerializedObject(component).GetIterator(); while (p.NextVisible(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.name != "m_Script") Assert.NotNull(p.objectReferenceValue, component.GetType().Name + "." + p.propertyPath);
            }
            Assert.That(Object.FindFirstObjectByType<PaxPlaytestSave>().SaveFile, Is.EqualTo("pax-frontier-" + AssetDatabase.AssetPathToGUID(PaxFrontierAuthoring.ScenePath) + ".xml"));
        }
        [Test] public void RepeatAndOverwriteRefusalPreserveAllScenesDefinitionsAndSettings()
        {
            var paths = Directory.GetFiles("Assets/PAX/Scenes", "*.unity").Concat(new[] { "ProjectSettings/EditorBuildSettings.asset", PaxFrontierAuthoring.DefinitionPath, PaxFrontierAuthoring.TerrainPath, PaxFrontierAuthoring.UiPath }).ToArray(); var before = paths.ToDictionary(p => p, File.ReadAllBytes);
            PaxFrontierAuthoring.EnsureFrontier(); Assert.Throws<IOException>(() => PaxFrontierAuthoring.CreateNewScene(definition, PaxFrontierAuthoring.ScenePath)); Assert.Throws<ArgumentException>(() => PaxFrontierAuthoring.CreateNewScene(definition, "Assets/../PaxFrontier.unity"));
            foreach (var p in paths) CollectionAssert.AreEqual(before[p], File.ReadAllBytes(p), p);
        }
        [Test] public void NewSceneActionCreatesIdenticalAuthoredWorldWithDistinctSlotAndPreservesPriorOutputs()
        {
            string path = "Assets/PAX/Scenes/FrontierValidation-" + Guid.NewGuid().ToString("N") + ".unity"; var settings = EditorBuildSettings.scenes; byte[] saved = File.ReadAllBytes(PaxFrontierAuthoring.ScenePath), original = File.ReadAllBytes(PaxResourceAuthoring.ScenePath), settingsBytes = File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset");
            EditorSceneManager.OpenScene(PaxFrontierAuthoring.ScenePath, OpenSceneMode.Single); string signature = Signature(Object.FindFirstObjectByType<PaxWorld>());
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<PaxFrontierDefinition>(PaxFrontierAuthoring.DefinitionPath); PaxFrontierAuthoring.CreateNewScene(source, path);
                var save = Object.FindFirstObjectByType<PaxPlaytestSave>(); Assert.AreEqual("pax-frontier-" + AssetDatabase.AssetPathToGUID(path) + ".xml", save.SaveFile); Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(PaxFrontierAuthoring.ScenePath), AssetDatabase.AssetPathToGUID(path));
                var manifest = Object.FindFirstObjectByType<PaxGeneratedFrontier>(); var expected = PaxFrontierLayout.Generate(manifest.Definition); var world = Object.FindFirstObjectByType<PaxWorld>(); CollectionAssert.AreEquivalent(expected.Tiles.Select(t => t.Cell), world.Blocks.Select(b => b.Cell));
                var actual = world.Blocks.ToDictionary(b => b.Cell); foreach (var t in expected.Tiles) { var b = actual[t.Cell]; Assert.AreEqual(t.Material.ToString(), b.SurfaceMaterial.name); Assert.AreEqual(t.Mineable, b.Mineable); }
                Assert.AreEqual(signature, Signature(world), "Identical definition produces the identical existing authored-world signature, independently of its distinct save slot.");
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(PaxFrontierAuthoring.ScenePath)); CollectionAssert.AreEqual(original, File.ReadAllBytes(PaxResourceAuthoring.ScenePath));
            }
            finally
            {
                EditorSceneManager.OpenScene(PaxFrontierAuthoring.ScenePath, OpenSceneMode.Single);
                // Flush the restored list BEFORE deleting the temporary scene: otherwise a later
                // SaveAssets can expose the pending temporary Build Settings entry to another test.
                EditorBuildSettings.scenes = settings; AssetDatabase.SaveAssets();
                AssetDatabase.DeleteAsset(path); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); AssetDatabase.SaveAssets();
            }
            CollectionAssert.AreEqual(settingsBytes, File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset"), "Temporary scene and build-list cleanup must be complete before this test returns.");
        }
        private static string Signature(PaxWorld world) => new PaxContinueSlot { minimum=world.Minimum, maximum=world.Maximum, tiles=world.Blocks.Select(b=>new PaxContinueSlot.Tile {cell=b.Cell, material=b.SurfaceMaterial, mineable=b.Mineable}).ToArray() }.SaveSignature;
    }
}
