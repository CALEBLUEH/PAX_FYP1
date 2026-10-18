using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxContinueTests
    {
        private string directory, path, bytes, backup, expected; private PaxPause pause; private PaxExpeditionSelection menu;
        private UnityAction<Scene, LoadSceneMode> focusHook;
        private static readonly string[] Scenes = { "PaxResourceExpedition", "PaxLamplighterChart", "PaxEmberReach" };
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        [UnitySetUp] public IEnumerator Setup() { Time.timeScale = 1; Time.captureDeltaTime = 1f / 60; directory = Path.Combine(Path.GetTempPath(), "PaxContinueTests", Guid.NewGuid().ToString("N")); yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (focusHook != null) SceneManager.sceneLoaded -= focusHook;
            foreach (var transfer in Object.FindObjectsByType<PaxContinueTransfer>(FindObjectsSortMode.None)) Object.Destroy(transfer.gameObject);
            if (pause != null) pause.enabled = false; Time.timeScale = 1; Time.captureDeltaTime = 0;
            if (Directory.Exists(directory)) { Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaxContinueTests")), Path.GetDirectoryName(Path.GetFullPath(directory))); Directory.Delete(directory, true); } yield return null;
        }
        private IEnumerator Seed(int number)
        {
            yield return SceneManager.LoadSceneAsync(Scenes[number - 1]); pause = Object.FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); Object.FindFirstObjectByType<PaxInput>().enabled = false; Object.FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            yield return Release(); yield return Frames(15); var storage = Object.FindFirstObjectByType<PaxPlaytestSave>(); storage.UseValidationDirectory(directory); var builder = Object.FindFirstObjectByType<PaxBuilder>(); var stocks = Object.FindFirstObjectByType<PaxStockpile>(); var mission = Object.FindFirstObjectByType<PaxExpedition>();
            foreach (var cell in Object.FindFirstObjectByType<PaxResourceCache>().Cells) Assert.IsTrue(builder.Mine(cell), cell.ToString()); Assert.IsTrue(storage.SaveEdits());
            var route = Object.FindFirstObjectByType<PaxChartRoute>(); if (route != null) { Object.FindFirstObjectByType<PaxPlayer>().Warp(route.NearBank.position); yield return Frames(3); }
            builder.TrySelectMaterial(number == 1 ? "stone" : "timber"); Assert.IsTrue(builder.Place(mission.Repairs[0].Cell), "Seed earns one repair through the validated builder"); Assert.IsTrue(storage.SaveEdits());
            path = storage.SavePath; bytes = File.ReadAllText(path); backup = File.ReadAllText(path + ".bak"); expected = stocks.Capture().Encode();
            Object.FindFirstObjectByType<PaxPlayer>().Warp(mission.Sites[0].transform.position); yield return Frames(35); Assert.IsTrue(mission.Sites[0].Surveyed);
        }
        private IEnumerator Menu()
        {
            yield return SceneManager.LoadSceneAsync("PaxExpeditionSelection"); menu = Object.FindFirstObjectByType<PaxExpeditionSelection>(); menu.UseValidationInput(); menu.UseValidationDirectory(directory);
            for (int i = 0; i < 3; i++) { menu.ProcessInput(false, false, false, false, false, false, false); yield return null; } menu.RefreshSaves(); yield return Frames(2);
        }
        private IEnumerator AwaitContinue(int number)
        {
            float until = Time.realtimeSinceStartup + 20; while (SceneManager.GetActiveScene().name != Scenes[number - 1] && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(Scenes[number - 1], SceneManager.GetActiveScene().name); pause = Object.FindFirstObjectByType<PaxPause>(); Object.FindFirstObjectByType<PaxInput>().enabled = false; Object.FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            var receiver = Object.FindFirstObjectByType<PaxContinueReceiver>(); while (!receiver.Finished && Time.realtimeSinceStartup < until) yield return null; Assert.IsTrue(receiver.Finished); yield return Frames(2);
            Assert.AreEqual(0, Object.FindObjectsByType<PaxContinueTransfer>(FindObjectsSortMode.None).Length);
        }
        private IEnumerator Release() { for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; } }
        private static void Submit(string name) { var button = Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Button>(name); button.Focus(); using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit); }
        [UnityTest] public IEnumerator ResourceMappingRestoresExactWorldStocksAndHistory() { yield return Mapping(1); }
        [UnityTest] public IEnumerator ChartMappingRestoresExactWorldStocksAndHistory() { yield return Mapping(2); }
        [UnityTest] public IEnumerator EmberMappingRestoresExactWorldStocksAndHistory() { yield return Mapping(3); }
        private IEnumerator Mapping(int number)
        {
            yield return Seed(number); yield return Menu(); Assert.NotNull(menu.ContinueCatalog, "Authored catalog must survive the scene switch"); Assert.IsTrue(menu.CanContinue(number), menu.ContinueReason(number) + " / catalog=" + menu.ContinueCatalog.Slots[number - 1].SaveSignature + " / saved=" + PaxResourceSnapshot.Decode(bytes).Tiles.World); for (int i = 1; i <= 3; i++) if (i != number) Assert.IsFalse(menu.CanContinue(i));
            Assert.AreEqual(bytes, File.ReadAllText(path)); Assert.AreEqual(backup, File.ReadAllText(path + ".bak")); string scenePath = menu.ContinueCatalog.Slots[number - 1].scenePath;
            menu.ProcessInput(false, false, false, true, number == 1, number == 2, number == 3); Assert.IsTrue(menu.Loading, "4/5/6 input frame must select the matching Continue slot"); Assert.IsFalse(menu.Continue(number)); yield return AwaitContinue(number);
            var stocks = Object.FindFirstObjectByType<PaxStockpile>(); var world = Object.FindFirstObjectByType<PaxWorld>(); Assert.IsTrue(Object.FindFirstObjectByType<PaxContinueReceiver>().Restored); Assert.AreEqual(expected, stocks.Capture().Encode()); Assert.AreEqual(scenePath, world.gameObject.scene.path);
            Assert.AreEqual(0, Object.FindFirstObjectByType<PaxExpedition>().SurveyedCount); Assert.AreEqual(bytes, File.ReadAllText(path)); Assert.AreEqual(backup, File.ReadAllText(path + ".bak"));
            yield return Release(); var original = Object.FindFirstObjectByType<PaxResourceCache>().Cells[0]; var builder = Object.FindFirstObjectByType<PaxBuilder>(); string id = number == 1 ? "stone" : "timber"; int before = stocks.Count(id); builder.TrySelectMaterial(id);
            Assert.IsTrue(builder.Place(original)); Assert.IsTrue(builder.Mine(original)); Assert.AreEqual(before, stocks.Count(id), "Restored harvest history prevents farming");
        }
        [UnityTest] public IEnumerator CorruptPrimaryRecoversOnlyItsCompatibleBackupWithoutAnyFileWrites()
        {
            yield return Seed(3); File.WriteAllText(path, "broken owned primary"); yield return Menu(); Assert.IsTrue(menu.CanContinue(3)); Assert.That(menu.ContinueReason(3), Does.Contain("Recoverable previous"));
            Submit("continue-ember"); yield return AwaitContinue(3); Assert.IsTrue(Object.FindFirstObjectByType<PaxContinueReceiver>().Restored); Assert.AreEqual(PaxResourceSnapshot.Decode(backup).Encode(), Object.FindFirstObjectByType<PaxStockpile>().Capture().Encode()); Assert.AreEqual("broken owned primary", File.ReadAllText(path)); Assert.AreEqual(backup, File.ReadAllText(path + ".bak")); Assert.That(Object.FindFirstObjectByType<PaxPlaytestSave>().Status, Does.Contain("previous compatible"));
        }
        [UnityTest] public IEnumerator MissingForgedWrongWorldAndWrongRulesNeverEnableContinueOrBlockFresh()
        {
            yield return Seed(3); File.Delete(path + ".bak"); yield return Menu();
            for (int variant = 0; variant < 4; variant++)
            {
                var data = PaxResourceSnapshot.Decode(bytes); if (variant == 0) data.Tiles.World = new string('0', 64); else if (variant == 1) data.Rules = new string('0', 64); else if (variant == 2) data.Counts["timber"]++; else { File.Delete(path); menu.RefreshSaves(); Assert.IsFalse(menu.CanContinue(3)); continue; }
                File.WriteAllText(path, data.Encode()); menu.RefreshSaves(); Assert.IsFalse(menu.CanContinue(3)); Assert.IsFalse(menu.Continue(3)); Assert.IsFalse(menu.Loading); Assert.IsTrue(menu.Ready);
            }
            Assert.IsTrue(menu.LaunchEmber()); float until = Time.realtimeSinceStartup + 20; while (SceneManager.GetActiveScene().name != "PaxEmberReach" && Time.realtimeSinceStartup < until) yield return null; pause = Object.FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); yield return Release(); yield return Frames(15); Assert.AreEqual(0, Object.FindFirstObjectByType<PaxStockpile>().Count("timber")); Assert.IsFalse(File.Exists(path));
        }
        [UnityTest] public IEnumerator ContinueRechecksFilesChangedAfterMenuSelectionAndKeepsFreshStateOnFailure()
        {
            yield return Seed(3); yield return Menu(); Assert.IsTrue(menu.CanContinue(3)); Assert.IsTrue(menu.Continue(3)); File.WriteAllText(path, "changed after selection"); File.Delete(path + ".bak"); yield return AwaitContinue(3);
            Assert.IsFalse(Object.FindFirstObjectByType<PaxContinueReceiver>().Restored); Assert.AreEqual(0, Object.FindFirstObjectByType<PaxStockpile>().Count("timber")); foreach (var cell in Object.FindFirstObjectByType<PaxResourceCache>().Cells) Assert.IsTrue(Object.FindFirstObjectByType<PaxWorld>().Contains(cell)); Assert.That(Object.FindFirstObjectByType<PaxPlaytestSave>().Status, Does.Contain("Continue rejected; fresh world kept")); Assert.AreEqual("changed after selection", File.ReadAllText(path)); yield return Release(); Assert.IsFalse(pause.BlocksActions);
        }
        [UnityTest] public IEnumerator SpawnOverlapRejectsValidPrimaryWithoutFallbackThenManualF9WorksAfterWalkingAside()
        {
            yield return Seed(3); var data = PaxResourceSnapshot.Decode(bytes); var camp = new Vector3Int(0, 0, -5); data.Tiles.Placed.Add(camp); data.Tiles.Materials.Add(camp, "timber"); data.Counts["timber"]--; string blocked = data.Encode(); File.WriteAllText(path, blocked); yield return Menu(); Assert.IsTrue(menu.CanContinue(3)); Assert.IsTrue(menu.Continue(3)); yield return AwaitContinue(3);
            var world = Object.FindFirstObjectByType<PaxWorld>(); var stocks = Object.FindFirstObjectByType<PaxStockpile>(); var player = Object.FindFirstObjectByType<PaxPlayer>(); Assert.IsFalse(Object.FindFirstObjectByType<PaxContinueReceiver>().Restored); Assert.AreEqual(0, stocks.Count("timber")); Assert.IsFalse(world.Contains(camp)); Assert.That(Object.FindFirstObjectByType<PaxPlaytestSave>().Status, Does.Contain("inside the player")); Assert.AreEqual(blocked, File.ReadAllText(path)); Assert.AreEqual(backup, File.ReadAllText(path + ".bak")); yield return Release();
            var rig = Object.FindFirstObjectByType<PaxCamera>(); var target = Object.FindFirstObjectByType<PaxChartRoute>().Departure.position;
            for (int i = 0; i < 180; i++) { var delta = target - player.transform.position; delta.y = 0; if (delta.magnitude < 0.09f) break; var f = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f); player.SetCommand(new Vector2(Vector3.Dot(delta.normalized, r), Vector3.Dot(delta.normalized, f)), false, false); yield return null; }
            player.SetCommand(Vector2.zero, false, false); Object.FindFirstObjectByType<PaxInput>().ApplyFrame(new PaxInputFrame { load = true }); Assert.IsTrue(world.Contains(camp)); Assert.AreEqual(data.Counts["timber"], stocks.Count("timber")); Assert.AreEqual(blocked, File.ReadAllText(path));
        }
        [UnityTest] public IEnumerator FocusPauseDuringContinueRequiresExplicitResumeAndHeldActionsCannotClickThrough()
        {
            yield return Seed(3); yield return Menu(); Time.timeScale = 0.65f;
            focusHook = (scene, mode) => { if (scene.name == "PaxEmberReach") Object.FindFirstObjectByType<PaxPause>().ProcessFocus(false); };
            SceneManager.sceneLoaded += focusHook; Assert.IsTrue(menu.Continue(3)); float until = Time.realtimeSinceStartup + 20; while (SceneManager.GetActiveScene().name != "PaxEmberReach" && Time.realtimeSinceStartup < until) yield return null; SceneManager.sceneLoaded -= focusHook; focusHook = null;
            pause = Object.FindFirstObjectByType<PaxPause>(); Object.FindFirstObjectByType<PaxInput>().enabled = false; yield return Frames(5); Assert.IsTrue(pause.IsPaused); Assert.AreEqual(0, Time.timeScale); Assert.IsFalse(Object.FindFirstObjectByType<PaxContinueReceiver>().Finished); Assert.AreEqual(0, Object.FindFirstObjectByType<PaxStockpile>().Count("timber")); pause.ProcessFocus(true); yield return Frames(3); Assert.IsFalse(Object.FindFirstObjectByType<PaxContinueReceiver>().Finished); Submit("pause-resume");
            yield return AwaitContinue(3); Assert.AreEqual(0.65f, Time.timeScale); int revision = Object.FindFirstObjectByType<PaxWorld>().Revision;
            for (int i = 0; i < 5; i++) { pause.ProcessInput(false, true); Object.FindFirstObjectByType<PaxInput>().ApplyFrame(new PaxInputFrame { mine = true, place = true, save = true, movement = Vector2.one, jumpPressed = true }); yield return null; } Assert.AreEqual(revision, Object.FindFirstObjectByType<PaxWorld>().Revision); Assert.AreEqual(bytes, File.ReadAllText(path)); yield return Release(); Assert.IsFalse(pause.BlocksActions);
            pause.Pause(); pause.RequestReturn(); yield return Release(); Submit("return-confirm"); until = Time.realtimeSinceStartup + 20; while (SceneManager.GetActiveScene().name != "PaxExpeditionSelection" && Time.realtimeSinceStartup < until) yield return null; Assert.AreEqual(0.65f, Time.timeScale); Assert.AreEqual(0, Object.FindObjectsByType<PaxContinueTransfer>(FindObjectsSortMode.None).Length);
        }
    }
}
