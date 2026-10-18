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
    public sealed class PaxGenerationTests
    {
        private PaxGenerationDefinition definition;
        [SetUp] public void Setup() => definition = ScriptableObject.CreateInstance<PaxGenerationDefinition>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(definition);
        private static string Snapshot(PaxIslandLayout layout) => string.Join(";", layout.Tiles.Select(tile => $"{tile.Cell}:{tile.Material}:{tile.Mineable}"));

        [Test]
        public void WrongUnityProjectIdentityIsRejectedBeforeAuthoring()
        {
            Assert.Throws<InvalidOperationException>(() => PaxIslandAuthoring.ValidateProjectIdentity(Path.GetTempPath()));
            PaxIslandAuthoring.ValidateProjectIdentity(Path.GetDirectoryName(Application.dataPath));
        }

        [Test]
        public void SameSeedIsExactAndDoesNotConsumeGlobalRandomState()
        {
            var state = UnityEngine.Random.state;
            var first = Snapshot(PaxIslandLayout.Generate(definition));
            Assert.AreEqual(state, UnityEngine.Random.state);
            Assert.AreEqual(first, Snapshot(PaxIslandLayout.Generate(definition)));
            definition.seed++;
            Assert.AreNotEqual(first, Snapshot(PaxIslandLayout.Generate(definition)));
        }

        [TestCase(12, 0)] [TestCase(12, 2)] [TestCase(16, 2)]
        public void SeedSweepHasUniqueBoundedTilesProtectedFoundationAndWalkableEntrances(int radius, int height)
        {
            definition.radius = radius; definition.terraceHeight = height;
            foreach (int seed in new[] { 0, -1, int.MinValue, int.MaxValue, 16427, 421 })
            {
                definition.seed = seed;
                var layout = PaxIslandLayout.Generate(definition);
                var cells = layout.Tiles.ToDictionary(tile => tile.Cell);
                foreach (var tile in layout.Tiles)
                {
                    Assert.IsTrue(tile.Cell.x >= layout.Minimum.x && tile.Cell.x <= layout.Maximum.x && tile.Cell.y >= layout.Minimum.y && tile.Cell.y <= layout.Maximum.y && tile.Cell.z >= layout.Minimum.z && tile.Cell.z <= layout.Maximum.z);
                    Assert.AreEqual(tile.Cell.y != -3, tile.Mineable);
                }
                var visited = new HashSet<Vector3Int>();
                var queue = new Queue<Vector3Int>();
                queue.Enqueue(new Vector3Int(0, 0, -3));
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    if (visited.Contains(cell) || !cells.ContainsKey(cell + Vector3Int.down) || cells.ContainsKey(cell) || cells.ContainsKey(cell + Vector3Int.up)) continue;
                    visited.Add(cell);
                    foreach (var step in new[] { Vector3Int.left, Vector3Int.right, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) }) queue.Enqueue(cell + step);
                }
                foreach (var structure in layout.Structures)
                {
                    Assert.IsTrue(visited.Contains(structure.door), $"Seed {seed}: unreachable {structure.kind} entrance");
                    Assert.IsTrue(visited.Contains(structure.centre), $"Seed {seed}: unreachable {structure.kind} interior");
                }
            }
        }

        [Test]
        public void StructureAndTerrainSettingsChangeLayoutAndInvalidValuesFail()
        {
            var full = PaxIslandLayout.Generate(definition);
            Assert.AreEqual(3, full.Structures.Count);
            Assert.IsTrue(full.Tiles.Any(tile => tile.Material == PaxTerrainMaterial.Grass && tile.Cell.y > -1));
            definition.house = definition.cave = definition.ruin = false;
            definition.terraceHeight = 0;
            var bare = PaxIslandLayout.Generate(definition);
            Assert.AreEqual(0, bare.Structures.Count);
            Assert.IsTrue(bare.Tiles.All(tile => tile.Cell.y < 0));
            definition.radius = 50;
            Assert.Throws<ArgumentException>(() => PaxIslandLayout.Generate(definition));
        }

        [Test]
        public void SavedSceneMatchesDefinitionHasWiringAndSeparateSaveSlot()
        {
            var scene = EditorSceneManager.OpenScene(PaxIslandAuthoring.ScenePath, OpenSceneMode.Single);
            var manifest = Object.FindFirstObjectByType<PaxGeneratedIsland>();
            var world = Object.FindFirstObjectByType<PaxWorld>();
            var expected = PaxIslandLayout.Generate(manifest.Definition);
            Assert.AreEqual(expected.Tiles.Count, world.Blocks.Count);
            Assert.AreEqual(world.Blocks.Count, manifest.AuthoredTileCount);
            CollectionAssert.AreEquivalent(expected.Tiles.Select(tile => tile.Cell), world.Blocks.Select(block => block.Cell));
            var actual = world.Blocks.ToDictionary(block => block.Cell);
            foreach (var tile in expected.Tiles)
            {
                Assert.AreEqual(tile.Material.ToString(), actual[tile.Cell].SurfaceMaterial.name);
                Assert.AreEqual(tile.Mineable, actual[tile.Cell].Mineable);
            }
            Assert.AreEqual(expected.Spawn, manifest.SpawnMarker.position);
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.NotNull(component, "Missing script");
                if (!component.GetType().Namespace.StartsWith("Pax")) continue;
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.name != "pause") Assert.NotNull(property.objectReferenceValue, component.GetType().Name + "." + property.propertyPath);
            }
            var save = Object.FindFirstObjectByType<PaxPlaytestSave>();
            Assert.That(Path.GetFileName(save.SavePath), Does.StartWith("pax-island-"));
            Assert.That(Path.GetFileNameWithoutExtension(save.SavePath).Length, Is.EqualTo("pax-island-".Length + 32));
            Assert.IsTrue(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == PaxIslandAuthoring.ScenePath));
        }

        [Test]
        public void RepeatedGenerationAndOverwriteRefusalKeepBothScenesAndBuildSettingsIdentical()
        {
            byte[] generated = File.ReadAllBytes(PaxIslandAuthoring.ScenePath), prototype = File.ReadAllBytes(PaxPrototypeAuthoring.ScenePath), settings = File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset");
            PaxIslandAuthoring.CreateTripIsland();
            Assert.Throws<IOException>(() => PaxIslandAuthoring.CreateNewScene(definition, PaxIslandAuthoring.ScenePath));
            CollectionAssert.AreEqual(generated, File.ReadAllBytes(PaxIslandAuthoring.ScenePath));
            CollectionAssert.AreEqual(prototype, File.ReadAllBytes(PaxPrototypeAuthoring.ScenePath));
            CollectionAssert.AreEqual(settings, File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset"));
        }
    }
}
