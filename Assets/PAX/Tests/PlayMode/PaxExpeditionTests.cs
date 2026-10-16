using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxExpeditionTests
    {
        private PaxExpedition mission;
        private PaxWorld world;
        private PaxPlayer player;
        private PaxBuilder builder;
        private PaxPlaytestSave session;
        private PaxCamera camera;
        private string directory;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.captureDeltaTime = 1f / 60;
            yield return SceneManager.LoadSceneAsync("PaxExpedition");
            Bind();
            directory = Path.Combine(Path.GetTempPath(), "PaxExpeditionTests", Guid.NewGuid().ToString("N"));
            session.UseValidationDirectory(directory);
            yield return Frames(15);
        }
        private void Bind()
        {
            mission = Object.FindFirstObjectByType<PaxExpedition>(); world = Object.FindFirstObjectByType<PaxWorld>();
            player = Object.FindFirstObjectByType<PaxPlayer>(); builder = Object.FindFirstObjectByType<PaxBuilder>();
            session = Object.FindFirstObjectByType<PaxPlaytestSave>(); camera = Object.FindFirstObjectByType<PaxCamera>();
            Object.FindFirstObjectByType<PaxInput>().enabled = false;
            Object.FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
            yield return null;
        }
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private IEnumerator Survey(bool reverse = false)
        {
            var sites = reverse ? mission.Sites.Reverse().ToArray() : mission.Sites;
            foreach (var site in sites) { player.Warp(site.transform.position); yield return Frames(40); }
        }
        private void BuildPlatform()
        {
            player.Warp(new Vector3(1, 0.03f, -3)); Physics.SyncTransforms();
            foreach (var marker in mission.Repairs.OrderBy(marker => marker.Cell.z).ThenBy(marker => marker.Cell.x)) Assert.IsTrue(builder.Place(marker.Cell), marker.Cell + ": " + builder.PlacementStatus);
        }
        private IEnumerator JumpToPlatform()
        {
            player.Warp(new Vector3(1, 0.03f, -3)); yield return Frames(10);
            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            var move = new Vector2(Vector3.Dot(Vector3.back, right), Vector3.Dot(Vector3.back, forward));
            player.SetCommand(move, true, true); yield return Frames(30);
            player.SetCommand(Vector2.zero, false, false); yield return Frames(35);
            Assert.IsTrue(player.Grounded); Assert.That(player.transform.position.y, Is.EqualTo(1).Within(0.06f));
            Assert.IsTrue(mission.AtBeacon, "Real jump must reach the beacon platform");
        }

        [UnityTest]
        public IEnumerator CameraRoofAndInterruptedShortVisitsCannotSurvey()
        {
            camera.enabled = false;
            camera.transform.position = mission.Sites[0].transform.position;
            yield return Frames(50); Assert.AreEqual(0, mission.SurveyedCount);
            camera.enabled = true;
            player.Warp(mission.Sites[0].transform.position + Vector3.up * 4);
            yield return Frames(45); Assert.AreEqual(0, mission.SurveyedCount, "Standing on the roof is not visiting its interior");
            player.Warp(mission.Sites[0].transform.position); yield return Frames(10);
            player.Warp(new Vector3(0, 0.03f, -3)); yield return Frames(15);
            player.Warp(mission.Sites[0].transform.position); yield return Frames(10);
            Assert.AreEqual(0, mission.SurveyedCount, "Separate brief contacts must not accumulate");
            yield return Frames(30); Assert.AreEqual(1, mission.SurveyedCount);
        }
        [UnityTest]
        public IEnumerator BothVisitOrdersAndRepeatedVisitsAreIdempotent()
        {
            foreach (bool reverse in new[] { false, true })
            {
                mission.RestartSurvey(); yield return Survey(reverse);
                Assert.AreEqual(3, mission.SurveyedCount); Assert.AreEqual(PaxExpeditionStage.Repair, mission.Stage);
                yield return Frames(50); Assert.AreEqual(3, mission.SurveyedCount);
                Assert.IsFalse(mission.TryActivate());
            }
        }
        [UnityTest]
        public IEnumerator EarlyRepairsUnrelatedTilesAndDisabledOrTriggerSolidsAreHandled()
        {
            Assert.IsTrue(builder.Place(new Vector3Int(-2, 0, -3))); yield return null;
            Assert.AreEqual(0, mission.RepairCount); Assert.IsFalse(mission.TryActivate());
            BuildPlatform(); yield return null;
            Assert.AreEqual(6, mission.RepairCount); Assert.AreEqual(PaxExpeditionStage.Survey, mission.Stage);
            Assert.IsFalse(mission.TryActivate());
            Assert.IsTrue(world.TryGet(mission.Repairs[0].Cell, out var block));
            block.Solid.enabled = false; Assert.AreEqual(5, mission.RepairCount);
            block.Solid.enabled = true; block.Solid.isTrigger = true; Assert.AreEqual(5, mission.RepairCount);
            block.Solid.isTrigger = false; Physics.SyncTransforms(); Assert.AreEqual(6, mission.RepairCount);
        }
        [UnityTest]
        public IEnumerator CompletionRequiresRealJumpReturnAndRepairRemovalRevokesIt()
        {
            yield return Survey(); BuildPlatform(); yield return null;
            Assert.AreEqual(PaxExpeditionStage.Return, mission.Stage); Assert.IsFalse(mission.TryActivate());
            yield return JumpToPlatform(); Assert.IsTrue(mission.TryActivate()); Assert.AreEqual(PaxExpeditionStage.Complete, mission.Stage);
            int revision = world.Revision; Assert.IsTrue(mission.TryActivate()); Assert.AreEqual(revision, world.Revision);
            player.Warp(new Vector3(1, 0.03f, -3)); yield return Frames(10);
            Assert.IsTrue(builder.Mine(new Vector3Int(1, 0, -5))); yield return null;
            Assert.AreEqual(PaxExpeditionStage.Repair, mission.Stage); Assert.IsFalse(mission.TryActivate());
            Assert.IsTrue(builder.Place(new Vector3Int(1, 0, -5))); yield return null;
            Assert.AreEqual(PaxExpeditionStage.Return, mission.Stage);
            Assert.IsTrue(builder.Place(new Vector3Int(-2, 0, -3)), "Sandbox remains usable after completion");
        }
        [UnityTest]
        public IEnumerator SavesPreviousSaveResetAndReloadDeriveRepairsWithoutSavingVisits()
        {
            BuildPlatform(); yield return null; Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(builder.Mine(new Vector3Int(1, 0, -5))); yield return null; Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(session.LoadEdits(true)); yield return null; Assert.AreEqual(6, mission.RepairCount); Assert.AreEqual(0, mission.SurveyedCount);
            yield return Survey(true);
            player.Warp(mission.BeaconReturn.position); yield return Frames(15); Assert.IsTrue(mission.TryActivate());
            var bytes = File.ReadAllBytes(session.SavePath); int revision = world.Revision;
            mission.RestartSurvey(); Assert.AreEqual(0, mission.SurveyedCount); Assert.AreEqual(6, mission.RepairCount); Assert.AreEqual(revision, world.Revision);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(session.SavePath));
            Assert.IsTrue(session.LoadEdits()); yield return null; Assert.AreEqual(5, mission.RepairCount);
            yield return Survey(); Assert.AreEqual(PaxExpeditionStage.Repair, mission.Stage);
            Assert.IsTrue(session.LoadEdits(true)); yield return null; Assert.AreEqual(PaxExpeditionStage.Return, mission.Stage);
            Assert.IsFalse(mission.TryActivate(), "Loading tiles must not remotely activate the beacon");
            yield return SceneManager.LoadSceneAsync("PaxExpedition"); Bind(); session.UseValidationDirectory(directory); yield return Frames(15);
            Assert.AreEqual(0, mission.SurveyedCount); Assert.AreEqual(0, mission.RepairCount); Assert.AreEqual(PaxExpeditionStage.Survey, mission.Stage);
            Assert.IsTrue(session.LoadEdits()); yield return null;
            Assert.AreEqual(5, mission.RepairCount); Assert.AreEqual(0, mission.SurveyedCount);
        }
        [UnityTest]
        public IEnumerator AuthoredHudShowsProgressCompletionAndSessionPersistenceNotice()
        {
            yield return new WaitForSecondsRealtime(0.2f);
            var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("expedition-repairs").text, Does.Contain("0/3"));
            Assert.That(root.Q<Label>("expedition-persistence").text, Does.Contain("tiles only"));
            yield return Survey(); BuildPlatform(); yield return JumpToPlatform(); Assert.IsTrue(mission.TryActivate());
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(root.Q<Label>("expedition-objective").text, Does.Contain("Signal restored"));
            Assert.That(root.Q<Label>("expedition-repairs").text, Does.Contain("6/6"));
            mission.RestartSurvey(); yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(root.Q<Label>("expedition-objective").text, Does.Contain("Survey"));
        }
    }
}
