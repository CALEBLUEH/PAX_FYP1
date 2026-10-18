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
    public sealed class PaxEntryTests
    {
        private string directory; private PaxPause pause; private PaxExpeditionSelection selection; private PaxPlaytestSave save; private PaxInput input;
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        [UnitySetUp] public IEnumerator Setup()
        { Time.timeScale = 1; Time.captureDeltaTime = 1f / 60; directory = Path.Combine(Path.GetTempPath(), "PaxEntryTests", Guid.NewGuid().ToString("N")); yield return Menu(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (pause != null) pause.enabled = false; Time.timeScale = 1; Time.captureDeltaTime = 0;
            if (Directory.Exists(directory)) { Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaxEntryTests")), Path.GetDirectoryName(Path.GetFullPath(directory))); Directory.Delete(directory, true); }
            yield return null;
        }
        private IEnumerator Menu()
        {
            yield return SceneManager.LoadSceneAsync("PaxExpeditionSelection"); selection = Object.FindFirstObjectByType<PaxExpeditionSelection>(); selection.UseValidationInput();
            yield return ReleaseMenu(); Assert.IsTrue(selection.Ready); Assert.IsNull(Object.FindFirstObjectByType<PaxPause>());
        }
        private IEnumerator ReleaseMenu() { for (int i = 0; i < 3; i++) { selection.ProcessInput(false, false, false); yield return null; } }
        private IEnumerator ReleasePause() { for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; } }
        private IEnumerator Game(bool chart, bool release = true)
        {
            string target = chart ? "PaxLamplighterChart" : "PaxResourceExpedition";
            for (int i = 0; i < 240 && SceneManager.GetActiveScene().name != target; i++) yield return null;
            Assert.AreEqual(target, SceneManager.GetActiveScene().name);
            pause = Object.FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); input = Object.FindFirstObjectByType<PaxInput>(); input.enabled = false;
            Object.FindFirstObjectByType<PaxExpeditionInput>().enabled = false; save = Object.FindFirstObjectByType<PaxPlaytestSave>(); save.UseValidationDirectory(directory);
            if (release) { yield return ReleasePause(); yield return Frames(15); } else yield return Frames(2);
            Assert.AreEqual(1, Object.FindObjectsByType<PaxPause>(FindObjectsSortMode.None).Length); Assert.IsNull(Object.FindFirstObjectByType<PaxExpeditionSelection>());
        }
        private static void Submit(string name)
        { var button = Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Button>(name); button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt); }
        private IEnumerator Returned()
        {
            for (int i = 0; i < 240 && SceneManager.GetActiveScene().name != "PaxExpeditionSelection"; i++) yield return null;
            Assert.AreEqual("PaxExpeditionSelection", SceneManager.GetActiveScene().name); selection = Object.FindFirstObjectByType<PaxExpeditionSelection>(); selection.UseValidationInput(); yield return ReleaseMenu();
        }
        [UnityTest] public IEnumerator BothKeyboardRoutesLaunchFreshAndRequireReleaseAfterFocus()
        {
            selection.ProcessFocus(false); Assert.IsFalse(selection.LaunchResource()); selection.ProcessInput(true, true, true); yield return Frames(2); Assert.AreEqual("PaxExpeditionSelection", SceneManager.GetActiveScene().name);
            selection.ProcessFocus(true); for (int i = 0; i < 3; i++) { selection.ProcessInput(true, false, true); yield return null; } Assert.IsFalse(selection.Ready);
            yield return ReleaseMenu(); selection.ProcessInput(true, false, true); Assert.IsFalse(selection.LaunchChart(), "One launch owns the transition"); yield return Game(false);
            Assert.AreEqual(0, Object.FindFirstObjectByType<PaxStockpile>().Count("stone")); Assert.AreEqual(0, Object.FindFirstObjectByType<PaxExpedition>().SurveyedCount);
            pause.Pause(); pause.RequestReturn(); yield return ReleasePause(); pause.ConfirmReturn(); yield return Returned();
            selection.ProcessInput(false, true, true); yield return Game(true); Assert.AreEqual(0, Object.FindFirstObjectByType<PaxStockpile>().Count("timber")); Assert.AreEqual(0, Object.FindFirstObjectByType<PaxExpedition>().SurveyedCount);
        }
        [UnityTest] public IEnumerator ResourceCancelPreservesLiveStateAndConfirmedReentryLoadsOnlyOnExplicitF9() { yield return Roundtrip(false); }
        [UnityTest] public IEnumerator ChartCancelPreservesLiveStateAndConfirmedReentryLoadsOnlyOnExplicitF9() { yield return Roundtrip(true); }
        private IEnumerator Roundtrip(bool chart)
        {
            Submit(chart ? "select-chart" : "select-resource"); yield return Game(chart);
            var world = Object.FindFirstObjectByType<PaxWorld>(); var player = Object.FindFirstObjectByType<PaxPlayer>(); var builder = Object.FindFirstObjectByType<PaxBuilder>(); var stocks = Object.FindFirstObjectByType<PaxStockpile>(); var mission = Object.FindFirstObjectByType<PaxExpedition>();
            var cells = Object.FindFirstObjectByType<PaxResourceCache>().Cells; Assert.IsTrue(builder.Mine(cells[0])); input.ApplyFrame(new PaxInputFrame { save = true }); Assert.IsTrue(File.Exists(save.SavePath));
            string path = save.SavePath, bytes = File.ReadAllText(path); string savedAccounting = stocks.Capture().Encode(); Assert.IsTrue(builder.Mine(cells[1]));
            player.Warp(mission.Sites[0].transform.position); yield return Frames(35); Assert.IsTrue(mission.Sites[0].Surveyed);
            pause.Pause(); string live = stocks.Capture().Encode(); int revision = world.Revision, surveyed = mission.SurveyedCount; Vector3 position = player.transform.position; float velocity = player.VerticalSpeed;
            Submit("pause-selection"); Assert.IsTrue(pause.ConfirmingReturn); pause.ConfirmReturn(); Assert.IsFalse(pause.Returning, "Confirmation never accepts the opening/held click");
            yield return ReleasePause(); var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement; Assert.AreEqual(root.Q<Button>("return-cancel"), root.focusController.focusedElement);
            Submit("return-cancel"); Assert.IsFalse(pause.ConfirmingReturn); Assert.IsTrue(pause.IsPaused); Assert.AreEqual(0, Time.timeScale); Assert.AreEqual(live, stocks.Capture().Encode()); Assert.AreEqual(revision, world.Revision); Assert.AreEqual(position, player.transform.position); Assert.AreEqual(velocity, player.VerticalSpeed); Assert.AreEqual(surveyed, mission.SurveyedCount); Assert.AreEqual(bytes, File.ReadAllText(path)); Assert.IsFalse(File.Exists(save.BackupPath));
            yield return ReleasePause(); Submit("pause-selection"); yield return ReleasePause(); pause.ProcessFocus(false); Submit("return-confirm"); Assert.IsFalse(pause.Returning); pause.ProcessFocus(true);
            pause.ConfirmReturn(); Assert.IsFalse(pause.Returning, "Refocus requires release"); yield return ReleasePause(); Submit("return-confirm"); yield return Returned(); Assert.AreEqual(1, Time.timeScale); Assert.AreEqual(bytes, File.ReadAllText(path)); Assert.IsFalse(File.Exists(path + ".bak"));
            Submit(chart ? "select-chart" : "select-resource"); yield return Game(chart); stocks = Object.FindFirstObjectByType<PaxStockpile>(); mission = Object.FindFirstObjectByType<PaxExpedition>(); world = Object.FindFirstObjectByType<PaxWorld>();
            Assert.IsTrue(world.Contains(cells[0])); Assert.IsTrue(world.Contains(cells[1])); Assert.AreEqual(0, stocks.Count(chart ? "timber" : "stone")); Assert.AreEqual(0, mission.SurveyedCount); Assert.AreEqual(path, save.SavePath); Assert.AreEqual(bytes, File.ReadAllText(path));
            input.ApplyFrame(new PaxInputFrame { load = true }); Assert.AreEqual(savedAccounting, stocks.Capture().Encode()); Assert.IsFalse(world.Contains(cells[0])); Assert.IsTrue(world.Contains(cells[1])); Assert.AreEqual(0, mission.SurveyedCount); Assert.AreEqual(bytes, File.ReadAllText(path));
        }
        [UnityTest] public IEnumerator EscapeCancelsConfirmationAndHeldKeysCannotResumeOrDiscard()
        {
            Assert.IsTrue(selection.LaunchResource()); yield return Game(false); pause.Pause(); pause.RequestReturn();
            for (int i = 0; i < 4; i++) { pause.ProcessInput(false, true); pause.ConfirmReturn(); yield return null; } Assert.IsFalse(pause.Returning); Assert.IsTrue(pause.ConfirmingReturn);
            yield return ReleasePause(); pause.ProcessInput(true, true); Assert.IsFalse(pause.ConfirmingReturn); Assert.IsTrue(pause.IsPaused);
            for (int i = 0; i < 4; i++) { pause.ProcessInput(true, true); yield return null; } Assert.IsTrue(pause.IsPaused); Assert.IsFalse(pause.Returning);
            yield return ReleasePause(); pause.ProcessInput(true, true); Assert.IsFalse(pause.IsPaused); Assert.IsTrue(pause.BlocksActions); yield return ReleasePause(); Assert.IsFalse(pause.BlocksActions);
        }
        [UnityTest] public IEnumerator ReturnRestoresNonUnitClockAndNewLaunchConsumesHeldGameplay()
        {
            Assert.IsTrue(selection.LaunchChart()); yield return Game(true); Time.timeScale = 0.6f; pause.Pause(); pause.RequestReturn(); yield return ReleasePause(); pause.ConfirmReturn(); yield return Returned(); Assert.AreEqual(0.6f, Time.timeScale);
            selection.ProcessInput(true, false, true); yield return Game(false, false); Assert.AreEqual(0.6f, Time.timeScale); var player = Object.FindFirstObjectByType<PaxPlayer>(); Vector3 position = player.transform.position;
            for (int i = 0; i < 4; i++) { pause.ProcessInput(false, true); Assert.IsTrue(pause.BlocksActions); input.ApplyFrame(new PaxInputFrame { movement = Vector2.one, jumpPressed = true, mine = true, place = true }); yield return null; }
            Assert.That(Vector3.Distance(position, player.transform.position), Is.LessThan(0.08f)); yield return ReleasePause(); input.ApplyFrame(new PaxInputFrame { movement = Vector2.up }); yield return Frames(5); Assert.That(Vector3.Distance(position, player.transform.position), Is.GreaterThan(0.1f));
        }
    }
}
