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
    public sealed class PaxPauseTests
    {
        private PaxPause pause; private PaxInput input; private PaxPlayer player; private PaxBuilder builder; private PaxCamera rig;
        private PaxExpedition mission; private PaxPlaytestSave save; private PaxStockpile stocks; private PaxWorld world; private string directory;
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private IEnumerator Open(string scene)
        {
            Time.captureDeltaTime = 1f / 60;
            yield return SceneManager.LoadSceneAsync(scene);
            pause = Object.FindFirstObjectByType<PaxPause>(); pause.UseValidationInput();
            input = Object.FindFirstObjectByType<PaxInput>(); input.enabled = false;
            Object.FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            player = Object.FindFirstObjectByType<PaxPlayer>(); builder = Object.FindFirstObjectByType<PaxBuilder>(); rig = Object.FindFirstObjectByType<PaxCamera>();
            mission = Object.FindFirstObjectByType<PaxExpedition>(); save = Object.FindFirstObjectByType<PaxPlaytestSave>(); stocks = Object.FindFirstObjectByType<PaxStockpile>(); world = Object.FindFirstObjectByType<PaxWorld>();
            directory = Path.Combine(Path.GetTempPath(), "PaxPauseTests", Guid.NewGuid().ToString("N")); save.UseValidationDirectory(directory);
            yield return Release(); yield return Frames(15);
        }
        private IEnumerator Release() { pause.ProcessInput(false, false); yield return null; pause.ProcessInput(false, false); yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (pause != null) pause.enabled = false; Time.timeScale = 1; Time.captureDeltaTime = 0;
            if (directory != null && Directory.Exists(directory)) { Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaxPauseTests")), Path.GetDirectoryName(Path.GetFullPath(directory))); Directory.Delete(directory, true); }
            yield return null;
        }
        [UnityTest] public IEnumerator ResourcePauseFreezesAirborneStateAndRejectsAllActions() { yield return Frozen("PaxResourceExpedition"); }
        [UnityTest] public IEnumerator ChartPauseFreezesAirborneStateAndRejectsAllActions() { yield return Frozen("PaxLamplighterChart"); }
        private IEnumerator Frozen(string scene)
        {
            yield return Open(scene); Assert.IsTrue(save.SaveEdits()); string bytes = File.ReadAllText(save.SavePath); string accounting = stocks.Capture().Encode();
            var cell = Object.FindFirstObjectByType<PaxResourceCache>().Cells[0];
            player.SetCommand(Vector2.zero, true, true); yield return Frames(5); Assert.Greater(player.VerticalSpeed, 0);
            builder.SetPointer(rig.View.WorldToScreenPoint(PaxGrid.Centre(cell)), true, false);
            pause.ProcessInput(true, true); Assert.IsTrue(pause.IsPaused);
            Vector3 position = player.transform.position, cameraPosition = rig.transform.position; Quaternion rotation = rig.transform.rotation;
            float velocity = player.VerticalSpeed, zoom = rig.View.orthographicSize, clock = Time.time;
            int revision = world.Revision, surveys = mission.SurveyedCount; string objective = mission.ActionStatus, material = builder.SelectedMaterialId, mode = builder.Mode;
            input.ApplyFrame(new PaxInputFrame { movement = Vector2.one, jumpPressed = true, jumpHeld = true, mine = true, place = true, orbit = 90, tilt = 15, zoom = 5, resetCamera = true, toggleCursor = true, toggleGhostTargets = true, respawn = true, save = true });
            player.Warp(Vector3.one * 99); rig.Orbit(90, 15); rig.Zoom(5); rig.ResetView(); builder.ToggleCursor(); builder.ToggleGhostTargets();
            Assert.IsFalse(builder.Mine(cell)); Assert.IsFalse(builder.Place(cell + Vector3Int.up)); Assert.IsFalse(builder.TrySelectMaterial("stone")); Assert.IsFalse(Object.FindFirstObjectByType<PaxMaterialSelection>().SelectNumber(3));
            Assert.IsFalse(save.SaveEdits()); Assert.IsFalse(save.LoadEdits()); Assert.IsFalse(save.LoadEdits(true)); Assert.IsFalse(mission.TryActivate()); mission.RestartSurvey();
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(position, player.transform.position); Assert.AreEqual(velocity, player.VerticalSpeed); Assert.AreEqual(clock, Time.time); Assert.AreEqual(cameraPosition, rig.transform.position); Assert.AreEqual(rotation, rig.transform.rotation); Assert.AreEqual(zoom, rig.View.orthographicSize);
            Assert.AreEqual(accounting, stocks.Capture().Encode()); Assert.AreEqual(revision, world.Revision); Assert.AreEqual(bytes, File.ReadAllText(save.SavePath)); Assert.IsFalse(File.Exists(save.BackupPath));
            Assert.AreEqual(surveys, mission.SurveyedCount); Assert.AreEqual(objective, mission.ActionStatus); Assert.AreEqual(material, builder.SelectedMaterialId); Assert.AreEqual(mode, builder.Mode);
            pause.ProcessInput(false, true); pause.ProcessInput(true, true); Assert.IsFalse(pause.IsPaused); Assert.IsTrue(pause.BlocksActions);
            for (int i = 0; i < 5; i++) { pause.ProcessInput(false, true); input.ApplyFrame(new PaxInputFrame { mine = true, place = true, jumpPressed = true, movement = Vector2.one }); yield return null; }
            Assert.AreEqual(revision, world.Revision); yield return Release(); yield return Frames(35); Assert.IsFalse(pause.BlocksActions); Assert.AreEqual(revision, world.Revision, "Queued/pre-resume mouse actions must not leak after release");
            Assert.IsTrue(builder.Mine(cell), "Fresh action works after releasing controls");
        }
        [UnityTest] public IEnumerator ResourceDiscoveryDwellStopsAndContinuesExactly() { yield return Dwell("PaxResourceExpedition"); }
        [UnityTest] public IEnumerator ChartDiscoveryDwellStopsAndContinuesExactly() { yield return Dwell("PaxLamplighterChart"); }
        private IEnumerator Dwell(string scene)
        {
            yield return Open(scene); var site = mission.Sites[0]; player.Warp(site.transform.position); yield return Frames(5);
            Assert.Greater(site.DwellSeconds, 0); Assert.IsFalse(site.Surveyed); float dwell = site.DwellSeconds;
            pause.Pause(); yield return new WaitForSecondsRealtime(0.6f); Assert.AreEqual(dwell, site.DwellSeconds); Assert.IsFalse(site.Surveyed);
            pause.Resume(); yield return Release(); yield return Frames(35); Assert.IsTrue(site.Surveyed);
        }
        [UnityTest] public IEnumerator ResourceAuthoredButtonConsumesClickAndRequiresRelease() { yield return ButtonAndFocus("PaxResourceExpedition"); }
        [UnityTest] public IEnumerator ChartAuthoredButtonConsumesClickAndRequiresRelease() { yield return ButtonAndFocus("PaxLamplighterChart"); }
        private IEnumerator ButtonAndFocus(string scene)
        {
            yield return Open(scene); int revision = world.Revision; pause.ProcessFocus(false); Assert.IsTrue(pause.IsPaused);
            pause.Resume(); Assert.IsTrue(pause.IsPaused, "Unfocused resume is rejected"); pause.ProcessFocus(true); Assert.IsTrue(pause.IsPaused);
            pause.ProcessInput(true, true); Assert.IsTrue(pause.IsPaused, "Held Escape on refocus is not a fresh press");
            var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement; yield return Frames(2);
            var button = root.Q<Button>("pause-resume"); Assert.Greater(button.worldBound.width, 100); Assert.AreEqual(DisplayStyle.Flex, root.Q("pause-overlay").resolvedStyle.display);
            Assert.AreEqual(button, root.focusController.focusedElement, "Returning focus restores UI keyboard focus without resuming");
            // Submit travels through the authored Button's real UI Toolkit event/clickable callback.
            using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
            Assert.IsFalse(pause.IsPaused); Assert.IsTrue(pause.BlocksActions);
            builder.SetPointer(rig.View.WorldToScreenPoint(PaxGrid.Centre(Object.FindFirstObjectByType<PaxResourceCache>().Cells[0])), true, true);
            yield return Frames(3); Assert.AreEqual(revision, world.Revision); pause.ProcessInput(false, true); Assert.IsTrue(pause.BlocksActions);
            yield return Release(); Assert.IsFalse(pause.BlocksActions); Assert.AreEqual(revision, world.Revision); Assert.AreEqual(DisplayStyle.None, root.Q("pause-overlay").resolvedStyle.display);
        }
        [UnityTest] public IEnumerator DisableRestoresOwnedNonUnitClockAndReenableDoesNotDuplicateButton()
        {
            yield return Open("PaxLamplighterChart"); Time.timeScale = 0.65f; pause.Pause(); Assert.AreEqual(0, Time.timeScale); pause.enabled = false; Assert.AreEqual(0.65f, Time.timeScale);
            pause.enabled = true; yield return Frames(2); pause.UseValidationInput(); pause.Pause();
            using (var submit = NavigationSubmitEvent.GetPooled()) Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Button>("pause-resume").SendEvent(submit);
            Assert.IsFalse(pause.IsPaused); Assert.AreEqual(0.65f, Time.timeScale);
        }
        [UnityTest] public IEnumerator UnloadRestoresClockAndNextFiniteSceneStartsUnpaused()
        {
            yield return Open("PaxResourceExpedition"); Time.timeScale = 0.75f; pause.Pause(); yield return SceneManager.LoadSceneAsync("PaxLamplighterChart");
            Assert.AreEqual(0.75f, Time.timeScale); pause = Object.FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); yield return Release(); Assert.IsFalse(pause.IsPaused); Assert.IsFalse(pause.BlocksActions);
        }
    }
}
