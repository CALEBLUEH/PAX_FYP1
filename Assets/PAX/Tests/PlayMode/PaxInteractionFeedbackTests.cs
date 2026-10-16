using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Pax.Tests
{
    public sealed class PaxInteractionFeedbackTests
    {
        private PaxWorld world;
        private PaxPlayer player;
        private PaxBuilder builder;
        private PaxCamera rig;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.captureDeltaTime = 1f / 60;
            yield return SceneManager.LoadSceneAsync("PaxPrototype");
            Object.FindFirstObjectByType<PaxInput>().enabled = false;
            world = Object.FindFirstObjectByType<PaxWorld>();
            player = Object.FindFirstObjectByType<PaxPlayer>();
            builder = Object.FindFirstObjectByType<PaxBuilder>();
            rig = Object.FindFirstObjectByType<PaxCamera>();
            builder.enabled = false;
            for (int i = 0; i < 10; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            yield return null;
        }

        [TestCase(0, 0, -3, PaxActionBlock.PlayerOverlap)]
        [TestCase(-3, 0, -3, PaxActionBlock.Occupied)]
        [TestCase(0, 4, -3, PaxActionBlock.Unsupported)]
        [TestCase(20, 0, 0, PaxActionBlock.OutsideWorld)]
        [TestCase(10, 0, -3, PaxActionBlock.OutOfReach)]
        public void InvalidPlacementExplainsItsSpecificCauseWithoutMutation(int x, int y, int z, PaxActionBlock reason)
        {
            int before = world.Revision;
            var cell = new Vector3Int(x, y, z);
            Assert.That(builder.EvaluatePlacement(cell), Is.EqualTo(reason));
            Assert.IsFalse(builder.Place(cell));
            Assert.That(world.Revision, Is.EqualTo(before));
            Assert.That(builder.LastAction, Does.Contain(builder.Explain(reason)));
        }

        [Test]
        public void ProtectedAndRemovedTargetsHaveActionableMiningFeedback()
        {
            Assert.That(builder.EvaluateMining(new Vector3Int(0, -2, -3)), Is.EqualTo(PaxActionBlock.Protected));
            Assert.IsFalse(builder.Mine(new Vector3Int(0, -2, -3)));
            Assert.That(builder.LastAction, Does.Contain("upper tile"));
            Assert.That(builder.EvaluateMining(new Vector3Int(-2, 1, -3)), Is.EqualTo(PaxActionBlock.MissingTile));
        }

        [Test]
        public void PhysicalObstructionExplainsRejectionEvenIfTheWallIsFaded()
        {
            Assert.IsTrue(world.TryAdd(new Vector3Int(-1, 0, -3)));
            Assert.IsTrue(world.TryGet(new Vector3Int(-1, 0, -3), out var wall));
            wall.Present(0, Color.cyan);
            Physics.SyncTransforms();
            Assert.That(builder.EvaluatePlacement(new Vector3Int(-2, 0, -3)), Is.EqualTo(PaxActionBlock.Occluded));
            Assert.IsFalse(builder.Place(new Vector3Int(-2, 0, -3)));
            Assert.That(builder.LastAction, Does.Contain("clear side"));
        }

        [UnityTest]
        public IEnumerator OutOfReachSurfaceRetainsRedPreviewAndExplainsRejectedClick()
        {
            builder.ToggleCursor();
            Vector2 point = rig.View.WorldToScreenPoint(PaxGrid.Centre(new Vector3Int(-6, 3, 9)));
            builder.Select(point);
            Assert.IsTrue(builder.CandidateCell.HasValue);
            Assert.IsNull(builder.SelectedCell);
            Assert.That(builder.MiningBlock, Is.EqualTo(PaxActionBlock.OutOfReach));
            Assert.IsTrue(builder.MiningPreview.gameObject.activeSelf);
            Assert.IsTrue(builder.MiningUsesInvalidMaterial);
            int before = world.Revision;
            builder.enabled = true;
            builder.SetPointer(point, true, false);
            yield return null;
            Assert.That(world.Revision, Is.EqualTo(before));
            Assert.That(builder.LastAction, Does.Contain("within 5 tiles"));
        }

        [Test]
        public void MovingIntoPreviewMakesItInvalidWithoutMovingThePredictedCell()
        {
            builder.ToggleCursor();
            Vector2 point = rig.View.WorldToScreenPoint(PaxGrid.Centre(new Vector3Int(-3, 1, -3)));
            builder.Select(point);
            Assert.IsTrue(builder.CanPlace);
            var predicted = builder.Placement;
            player.Warp(PaxGrid.Centre(predicted) - Vector3.up * 0.5f);
            Physics.SyncTransforms();
            builder.Select(point);
            Assert.That(builder.Placement, Is.EqualTo(predicted));
            Assert.That(builder.PlacementBlock, Is.EqualTo(PaxActionBlock.PlayerOverlap));
            Assert.IsFalse(builder.PlacementUsesValidMaterial);
            Assert.That(builder.PlacementPreview.position, Is.EqualTo(PaxGrid.Centre(predicted)));
        }

        [UnityTest]
        public IEnumerator AuthoredHudShowsSeparateReasonsAndClickFeedback()
        {
            builder.ToggleCursor();
            builder.Select(rig.View.WorldToScreenPoint(PaxGrid.Centre(new Vector3Int(-3, 1, -3))));
            Assert.IsFalse(builder.Place(new Vector3Int(0, 0, -3)));
            for (int i = 0; i < 15; i++) yield return null;
            var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("mining").text, Is.EqualTo(builder.MiningStatus));
            Assert.That(root.Q<Label>("placement").text, Is.EqualTo(builder.PlacementStatus));
            Assert.That(root.Q<Label>("action").text, Does.Contain("Step aside"));
        }
    }
}
