using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxEmberTests
    {
        private PaxPlayer player; private PaxCamera rig; private PaxBuilder builder; private PaxWorld world; private PaxChartRoute route; private PaxStockpile stocks; private PaxExpedition mission; private PaxPause pause; private PaxPlaytestSave save; private string directory;
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        [UnitySetUp] public IEnumerator Setup() { Time.captureDeltaTime = 1f / 60; directory = Path.Combine(Path.GetTempPath(), "PaxEmberTests", Guid.NewGuid().ToString("N")); yield return SceneManager.LoadSceneAsync("PaxEmberReach"); yield return Bind(); }
        private IEnumerator Bind()
        {
            pause = Object.FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); Object.FindFirstObjectByType<PaxInput>().enabled = false; Object.FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            player = Object.FindFirstObjectByType<PaxPlayer>(); rig = Object.FindFirstObjectByType<PaxCamera>(); builder = Object.FindFirstObjectByType<PaxBuilder>(); world = Object.FindFirstObjectByType<PaxWorld>(); route = Object.FindFirstObjectByType<PaxChartRoute>(); stocks = Object.FindFirstObjectByType<PaxStockpile>(); mission = Object.FindFirstObjectByType<PaxExpedition>(); save = Object.FindFirstObjectByType<PaxPlaytestSave>(); save.UseValidationDirectory(directory);
            for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; } yield return Frames(15);
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (pause != null) pause.enabled = false; Time.timeScale = 1; Time.captureDeltaTime = 0; if (Directory.Exists(directory)) { Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaxEmberTests")), Path.GetDirectoryName(Path.GetFullPath(directory))); Directory.Delete(directory, true); } yield return null; }
        private void Gather() { foreach (var cell in Object.FindFirstObjectByType<PaxResourceCache>().Cells) Assert.IsTrue(builder.Mine(cell), cell.ToString()); Assert.AreEqual(5, stocks.Count("timber")); Assert.AreEqual(2, stocks.Count("stone")); }
        private IEnumerator Walk(Vector3 target)
        {
            for (int i = 0; i < 240; i++) { var delta = target - player.transform.position; delta.y = 0; if (delta.magnitude < 0.09f) { player.SetCommand(Vector2.zero, false, false); yield break; } var f = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f); player.SetCommand(new Vector2(Vector3.Dot(delta.normalized, r), Vector3.Dot(delta.normalized, f)), false, false); yield return null; }
            player.SetCommand(Vector2.zero, false, false); Assert.Fail("Ember walk blocked at " + player.transform.position + " toward " + target);
        }
        private IEnumerator Click(Vector3 surface, PaxRepairMarker marker)
        { rig.SnapToTarget(); builder.SetPointer(rig.View.WorldToScreenPoint(surface), false, true); yield return Frames(3); Assert.IsTrue(marker.IsSolid, marker.Cell + ": " + builder.LastAction); }
        [UnityTest] public IEnumerator RidgeCompletionUsesActualNorthEastBuildWalkAndTwoJumpsWithoutWarp()
        {
            Gather(); yield return Walk(route.Departure.position); yield return Frames(35); Assert.IsTrue(mission.Sites[0].Surveyed); yield return Walk(route.NearBank.position);
            builder.ToggleCursor(); rig.Orbit(180, 0); builder.TrySelectMaterial("timber");
            for (int i = 0; i < 5; i++)
            {
                if (i == 2) { yield return Walk(route.Mooring.position); yield return Frames(35); Assert.IsTrue(mission.Sites[1].Surveyed); }
                var marker = route.Timber[i]; var direction = i < 2 ? Vector3Int.forward : Vector3Int.right;
                yield return Click(PaxGrid.Centre(marker.Cell - direction) + (Vector3)direction * 0.501f, marker); yield return Walk(PaxGrid.Centre(marker.Cell) + Vector3.up * 0.53f); Assert.IsTrue(player.Grounded); Assert.AreEqual(4 - i, stocks.Count("timber"));
            }
            Assert.IsTrue(save.SaveEdits()); rig.ResetView(); builder.TrySelectMaterial("stone"); foreach (var marker in route.Stone) yield return Click((Vector3)marker.Cell + new Vector3(0, 0.02f, 0), marker);
            for (int jump = 0; jump < 2; jump++) { var f = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f); player.SetCommand(new Vector2(Vector3.Dot(Vector3.right, r), Vector3.Dot(Vector3.right, f)), true, true); yield return Frames(jump == 0 ? 13 : 34); player.SetCommand(Vector2.zero, false, false); yield return Frames(45); Assert.IsTrue(player.Grounded); Assert.That(player.transform.position.y, Is.EqualTo(jump + 1).Within(0.08f)); }
            yield return Walk(route.Chart.position); yield return Frames(35); Assert.AreEqual(3, mission.SurveyedCount); Assert.IsTrue(mission.TryActivate()); yield return Frames(2); Assert.IsTrue(Object.FindFirstObjectByType<PaxCompletionLamps>().Complete); Assert.AreEqual(7, mission.RepairCount); Assert.AreEqual(0, stocks.Count("stone")); Assert.That(mission.Objective, Does.Contain("home offered forward")); Assert.IsTrue(save.SaveEdits());
            Assert.IsTrue(save.LoadEdits(true)); yield return Frames(2); Assert.AreEqual(2, stocks.Count("stone")); Assert.AreEqual(5, mission.RepairCount); Assert.IsFalse(Object.FindFirstObjectByType<PaxCompletionLamps>().Complete);
            for (int i = 0; i < 3; i++) Assert.IsTrue(save.LoadEdits()); Assert.AreEqual(0, stocks.Count("stone")); Assert.AreEqual(0, stocks.Count("timber")); Assert.IsTrue(mission.TryActivate());
            string bytes = File.ReadAllText(save.SavePath); mission.RestartSurvey(); yield return Frames(2); Assert.AreEqual(0, mission.SurveyedCount); Assert.AreEqual(7, mission.RepairCount); Assert.IsFalse(Object.FindFirstObjectByType<PaxCompletionLamps>().Complete); Assert.AreEqual(bytes, File.ReadAllText(save.SavePath));
        }
        [UnityTest] public IEnumerator MixedRetryAndRepeatedHarvestRebuildCannotMintSupplies()
        {
            Gather(); var original = Object.FindFirstObjectByType<PaxResourceCache>().Cells[0]; builder.TrySelectMaterial("timber");
            for (int i = 0; i < 3; i++) { Assert.IsTrue(builder.Place(original)); Assert.AreEqual(4, stocks.Count("timber")); Assert.IsTrue(builder.Mine(original)); Assert.AreEqual(5, stocks.Count("timber")); }
            yield return Walk(route.NearBank.position); var first = route.Timber[0]; builder.TrySelectMaterial("stone"); Assert.IsTrue(builder.Place(first.Cell)); Assert.IsFalse(first.IsSolid); Assert.IsTrue(save.SaveEdits()); Assert.IsTrue(builder.Mine(first.Cell)); Assert.AreEqual(2, stocks.Count("stone")); builder.TrySelectMaterial("timber"); Assert.IsTrue(builder.Place(first.Cell)); Assert.IsTrue(first.IsSolid); Assert.IsTrue(save.SaveEdits());
            Assert.IsTrue(save.LoadEdits(true)); Assert.IsFalse(first.IsSolid); Assert.AreEqual(5, stocks.Count("timber")); Assert.AreEqual(1, stocks.Count("stone")); for (int i = 0; i < 3; i++) Assert.IsTrue(save.LoadEdits()); Assert.IsTrue(first.IsSolid); Assert.AreEqual(4, stocks.Count("timber")); Assert.AreEqual(2, stocks.Count("stone"));
        }
        [UnityTest] public IEnumerator ThirdSelectionKeyAndButtonReturnToIndependentFreshSlot()
        {
            yield return SceneManager.LoadSceneAsync("PaxExpeditionSelection"); var menu = Object.FindFirstObjectByType<PaxExpeditionSelection>(); menu.UseValidationInput(); menu.ProcessFocus(false); Assert.IsFalse(menu.LaunchEmber()); menu.ProcessFocus(true);
            for (int i = 0; i < 3; i++) { menu.ProcessInput(false, false, true, true); yield return null; } Assert.IsFalse(menu.Ready); for (int i = 0; i < 3; i++) { menu.ProcessInput(false, false, false, false); yield return null; }
            menu.ProcessInput(false, false, true, true); yield return WaitScene("PaxEmberReach"); yield return Bind(); var cell = Object.FindFirstObjectByType<PaxResourceCache>().Cells[0]; Assert.IsTrue(builder.Mine(cell)); Assert.IsTrue(save.SaveEdits()); string path = save.SavePath, bytes = File.ReadAllText(path); Assert.IsTrue(builder.Mine(Object.FindFirstObjectByType<PaxResourceCache>().Cells[1]));
            pause.Pause(); pause.RequestReturn(); for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; } Submit("return-confirm"); yield return WaitScene("PaxExpeditionSelection"); menu = Object.FindFirstObjectByType<PaxExpeditionSelection>(); menu.UseValidationInput(); for (int i = 0; i < 3; i++) { menu.ProcessInput(false, false, false, false); yield return null; }
            Submit("select-ember"); yield return WaitScene("PaxEmberReach"); yield return Bind(); Assert.AreEqual(path, save.SavePath); Assert.AreEqual(0, stocks.Count("timber")); Assert.IsTrue(world.Contains(cell)); Assert.AreEqual(0, mission.SurveyedCount); Assert.AreEqual(bytes, File.ReadAllText(path)); Object.FindFirstObjectByType<PaxInput>().ApplyFrame(new PaxInputFrame { load = true }); Assert.AreEqual(1, stocks.Count("timber")); Assert.IsFalse(world.Contains(cell));
        }
        private static void Submit(string name) { var button = Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Button>(name); button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt); }
        private static IEnumerator WaitScene(string name) { for (int i = 0; i < 240 && SceneManager.GetActiveScene().name != name; i++) yield return null; Assert.AreEqual(name, SceneManager.GetActiveScene().name); }
    }
}
