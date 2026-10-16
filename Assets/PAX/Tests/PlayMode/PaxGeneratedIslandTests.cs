using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxGeneratedIslandTests
    {
        private PaxWorld world;
        private PaxPlayer player;
        private PaxGeneratedIsland island;
        private string directory;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.captureDeltaTime = 1f / 60;
            yield return SceneManager.LoadSceneAsync("PaxGeneratedIsland");
            Object.FindFirstObjectByType<PaxInput>().enabled = false;
            world = Object.FindFirstObjectByType<PaxWorld>();
            player = Object.FindFirstObjectByType<PaxPlayer>();
            island = Object.FindFirstObjectByType<PaxGeneratedIsland>();
            for (int i = 0; i < 15; i++) yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
            directory = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredSpawnAndEveryTwoHighDoorHaveRealCollisionAndCutaway()
        {
            Assert.IsTrue(player.Grounded);
            Assert.That(player.transform.position.y, Is.EqualTo(0).Within(0.06f));
            var camera = Object.FindFirstObjectByType<PaxCamera>();
            var cutaway = Object.FindFirstObjectByType<PaxCutaway>();
            foreach (var structure in island.Structures)
            {
                player.Warp((Vector3)structure.door + new Vector3(0, 0.03f, -1));
                player.enabled = false;
                var body = player.GetComponent<CharacterController>();
                for (int i = 0; i < 45; i++) { body.Move(new Vector3(0, -2f / 60, 4.5f / 60)); yield return null; }
                Assert.That(player.transform.position.z, Is.GreaterThan(structure.door.z + 1), structure.kind + " doorway blocked");
                // The opposite wall must physically stop the controller.
                for (int i = 0; i < 55; i++) { body.Move(new Vector3(0, -2f / 60, 4.5f / 60)); yield return null; }
                Assert.That(player.transform.position.z, Is.LessThan(structure.centre.z + 1.1f), structure.kind + " wall collision missing");
                player.enabled = true;
                player.Warp((Vector3)structure.centre + Vector3.up * 0.03f);
                camera.SnapToTarget();
                for (int i = 0; i < 20; i++) yield return null;
                cutaway.RefreshNow();
                if (structure.kind != "Ruin") Assert.That(cutaway.RoofCoverage, Is.GreaterThan(0));
                foreach (var block in world.Blocks)
                {
                    Assert.IsTrue(block.Solid.enabled);
                    Assert.NotNull(block.SolidShadow);
                    if (cutaway.IsObstruction(block)) Assert.That(cutaway.TargetFor(block), Is.LessThanOrEqualTo(0.04f));
                }
            }
        }

        [UnityTest]
        public IEnumerator MiningPlacementAndSaveRoundTripPreserveGeneratedMaterialsAndSolids()
        {
            var builder = Object.FindFirstObjectByType<PaxBuilder>();
            var session = Object.FindFirstObjectByType<PaxPlaytestSave>();
            directory = Path.Combine(Path.GetTempPath(), "PaxGeneratedTests", Guid.NewGuid().ToString("N"));
            session.UseValidationDirectory(directory);
            var cell = new Vector3Int(-2, 0, -3);
            Assert.IsTrue(builder.Place(cell));
            Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(builder.Mine(cell));
            Assert.IsTrue(session.LoadEdits());
            Assert.IsTrue(world.TryGet(cell, out var restored));
            Assert.IsTrue(restored.Solid.enabled); Assert.NotNull(restored.SolidShadow);
            var terrain = new Vector3Int(1, -1, -3);
            Assert.AreEqual(PaxActionBlock.Protected, builder.EvaluateMining(new Vector3Int(1, -3, -3)));
            Assert.IsTrue(builder.Mine(terrain), "Nearest terrain top must be reachable for downward mining");
            Assert.IsFalse(world.Contains(terrain));
            Assert.IsTrue(session.LoadEdits());
            Assert.IsTrue(world.TryGet(terrain, out var ground));
            Assert.AreEqual("Grass", ground.SurfaceMaterial.name);
            Assert.IsTrue(ground.Solid.enabled);
            var roof = island.Structures.First(structure => structure.kind == "House");
            var roofCell = roof.centre + Vector3Int.up * roof.roofHeight;
            Assert.IsTrue(world.TryGet(roofCell, out var original));
            var material = original.SurfaceMaterial;
            Assert.IsTrue(world.TryMine(roofCell));
            Assert.IsTrue(session.LoadEdits());
            Assert.IsTrue(world.TryGet(roofCell, out var rebuilt));
            Assert.AreEqual(material, rebuilt.SurfaceMaterial);
            int revision = world.Revision;
            Assert.IsTrue(session.LoadEdits()); Assert.AreEqual(revision, world.Revision);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DifferentAuthoredWorldRejectsSaveAndEditingSettingsDoesNotRegenerate()
        {
            var signature = world.SaveSignature;
            Assert.AreEqual(0, world.CaptureEdits().Placed.Count + world.CaptureEdits().Removed.Count);
            var cell = new Vector3Int(-2, 0, -3);
            Assert.IsTrue(world.TryAdd(cell));
            int seed = island.Definition.seed;
            try
            {
                island.Definition.seed++;
                yield return null;
                Assert.AreEqual(signature, world.SaveSignature);
                Assert.IsTrue(world.Contains(cell));
                Assert.AreEqual(seed, island.AuthoredSeed);
            }
            finally { island.Definition.seed = seed; }
            var edits = world.CaptureEdits();
            yield return SceneManager.LoadSceneAsync("PaxPrototype");
            world = Object.FindFirstObjectByType<PaxWorld>();
            int revision = world.Revision;
            Assert.IsFalse(world.TryApplyEdits(edits, new Bounds(Vector3.one * 100, Vector3.one), out _));
            Assert.AreEqual(revision, world.Revision);
        }
    }
}
