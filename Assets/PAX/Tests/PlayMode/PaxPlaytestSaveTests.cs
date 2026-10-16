using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxPlaytestSaveTests
    {
        private PaxWorld world;
        private PaxPlayer player;
        private PaxBuilder builder;
        private PaxPlaytestSave session;
        private string directory;
        private static readonly string Parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaxPlaytestSaveTests"));
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.captureDeltaTime = 1f / 60;
            yield return SceneManager.LoadSceneAsync("PaxPrototype");
            Object.FindFirstObjectByType<PaxInput>().enabled = false;
            world = Object.FindFirstObjectByType<PaxWorld>();
            player = Object.FindFirstObjectByType<PaxPlayer>();
            builder = Object.FindFirstObjectByType<PaxBuilder>();
            session = Object.FindFirstObjectByType<PaxPlaytestSave>();
            Assert.NotNull(session);
            directory = Path.Combine(Parent, Guid.NewGuid().ToString("N"));
            session.UseValidationDirectory(directory);
            for (int i = 0; i < 10; i++) yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            if (directory != null && Directory.Exists(directory))
            {
                Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)), Is.EqualTo(Parent));
                Directory.Delete(directory, true);
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator SaveLoadAndPreviousSaveRestoreTilesMaterialsAndPhysics()
        {
            var placed = new Vector3Int(-2, 0, -3);
            var rock = new Vector3Int(-3, 1, -3);
            Assert.IsTrue(world.TryGet(rock, out var original));
            Material rockMaterial = original.SurfaceMaterial;
            Assert.IsTrue(builder.Place(placed));
            Assert.IsTrue(session.SaveEdits()); // A: placed tile, original rock intact.
            Assert.IsTrue(builder.Mine(rock));
            Assert.IsTrue(session.SaveEdits()); // B: rock removed; A is the previous save.
            Assert.IsTrue(builder.Mine(placed));
            Assert.IsTrue(session.LoadEdits());
            Assert.IsTrue(world.Contains(placed)); Assert.IsFalse(world.Contains(rock));
            Assert.IsTrue(session.LoadEdits(true));
            Assert.IsTrue(world.TryGet(rock, out var restored));
            Assert.That(restored.SurfaceMaterial, Is.EqualTo(rockMaterial));
            Assert.IsTrue(restored.Solid.enabled); Assert.NotNull(restored.SolidShadow);
            int revision = world.Revision;
            Assert.IsTrue(session.LoadEdits(true));
            Assert.That(world.Revision, Is.EqualTo(revision), "Repeated load must not churn unchanged world objects");
            yield return null;
        }
        [UnityTest]
        public IEnumerator MalformedPrimaryKeepsWorldAndPreviousSaveCanRecover()
        {
            Assert.IsTrue(builder.Place(new Vector3Int(-2, 0, -3)));
            Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(builder.Mine(new Vector3Int(-2, 0, -3)));
            Assert.IsTrue(session.SaveEdits());
            File.WriteAllText(session.SavePath, "<broken");
            int revision = world.Revision;
            Assert.IsFalse(session.LoadEdits());
            Assert.That(world.Revision, Is.EqualTo(revision));
            Assert.IsFalse(world.Contains(new Vector3Int(-2, 0, -3)));
            Assert.That(session.Status, Does.Contain("current world kept"));
            Assert.IsTrue(session.LoadEdits(true));
            Assert.IsTrue(world.Contains(new Vector3Int(-2, 0, -3)));
            Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(File.Exists(session.BackupPath));
            Assert.That(Directory.GetFiles(directory, "*.invalid-*").Length, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest]
        public IEnumerator SemanticallyInvalidPrimaryCannotReplaceTheRecoverableBackup()
        {
            var cell = new Vector3Int(-2, 0, -3);
            Assert.IsTrue(builder.Place(cell));
            Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(builder.Mine(cell));
            Assert.IsTrue(session.SaveEdits());
            var invalid = world.CaptureEdits();
            invalid.Removed.Add(new Vector3Int(0, -2, -3));
            File.WriteAllText(session.SavePath, PaxSaveFile.Encode(invalid));
            Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(session.LoadEdits(true));
            Assert.IsTrue(world.Contains(cell), "The protected-removal primary must not overwrite the previous valid backup");
            Assert.That(Directory.GetFiles(directory, "*.invalid-*").Length, Is.EqualTo(1));
            yield return null;
        }
        [TestCase("protected")]
        [TestCase("duplicate-removed")]
        [TestCase("duplicate-placed")]
        [TestCase("outside")]
        [TestCase("wrong-world")]
        [TestCase("occupied")]
        public void InvalidWorldEditsNeverPartiallyMutateTheLiveGrid(string scenario)
        {
            var data = world.CaptureEdits();
            switch (scenario)
            {
                case "protected": data.Removed.Add(new Vector3Int(0, -2, -3)); break;
                case "duplicate-removed": data.Removed.Add(new Vector3Int(-3, 1, -3)); data.Removed.Add(new Vector3Int(-3, 1, -3)); break;
                case "duplicate-placed": data.Placed.Add(new Vector3Int(-2, 0, -3)); data.Placed.Add(new Vector3Int(-2, 0, -3)); break;
                case "outside": data.Placed.Add(new Vector3Int(100, 100, 100)); break;
                case "wrong-world": data.World = new string('B', 64); break;
                case "occupied": data.Placed.Add(new Vector3Int(0, -1, -3)); break;
            }
            int revision = world.Revision, count = world.Blocks.Count;
            Assert.IsFalse(world.TryApplyEdits(data, player.BodyBounds, out string reason));
            Assert.IsNotEmpty(reason);
            Assert.That(world.Revision, Is.EqualTo(revision));
            Assert.That(world.Blocks.Count, Is.EqualTo(count));
        }
        [UnityTest]
        public IEnumerator RestoredSolidsCannotBeLoadedInsideThePlayer()
        {
            var data = world.CaptureEdits();
            data.Placed.Add(new Vector3Int(0, 0, -3));
            PaxSaveFile.Write(session.SavePath, data);
            int revision = world.Revision;
            Assert.IsFalse(session.LoadEdits());
            Assert.That(world.Revision, Is.EqualTo(revision));
            Assert.That(session.Status, Does.Contain("Step aside"));
            player.Warp(new Vector3(0, 0.03f, -1));
            Assert.IsTrue(session.LoadEdits());
            Assert.IsTrue(world.Contains(new Vector3Int(0, 0, -3)));
            yield return null;
        }
        [UnityTest]
        public IEnumerator LoadingRoofEditsInvalidatesVisibilityAndLightAnalysis()
        {
            player.Warp(new Vector3(6, 0.03f, -7));
            Assert.IsTrue(builder.Mine(new Vector3Int(6, 3, -7)));
            Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(builder.Place(new Vector3Int(6, 3, -7)));
            var cutaway = Object.FindFirstObjectByType<PaxCutaway>();
            cutaway.RefreshNow(); float coverage = cutaway.RoofCoverage;
            Assert.IsTrue(session.LoadEdits());
            yield return null;
            Assert.That(cutaway.RoofCoverage, Is.LessThan(coverage));
            Assert.IsFalse(world.Contains(new Vector3Int(6, 3, -7)));
        }
    }
}
