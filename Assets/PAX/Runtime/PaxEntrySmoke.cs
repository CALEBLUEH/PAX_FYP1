using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pax
{
    // Explicit -paxSmoke -paxEntrySmoke only. Owns no normal player flow or save path.
    [DefaultExecutionOrder(200)]
    public sealed class PaxEntrySmoke : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>(); private int failures;
        private string output, slots; private PaxExpeditionSelection selection; private PaxPause pause; private PaxInput input; private PaxPlaytestSave save;
        private Camera cameraView; private UIDocument document; private PanelSettings originalPanel, panel; private RenderTexture target;
        private void Check(bool ok, string text) { checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; }
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); output = Path.GetFullPath(Path.Combine(Application.dataPath, "../SmokeEvidence")); Directory.CreateDirectory(output);
            slots = Path.Combine(output, "EntrySlots-" + Guid.NewGuid().ToString("N")); Time.captureDeltaTime = 1f / 60;
            yield return new WaitForSecondsRealtime(3.5f); yield return Menu();
            Check(SceneManager.GetActiveScene().name == "PaxExpeditionSelection", "Windows review build opens the authored expedition selection");
            selection.ProcessFocus(false); Check(!selection.LaunchResource() && !selection.LaunchChart(), "Unfocused selection rejects either launch");
            selection.ProcessFocus(true); for (int i = 0; i < 3; i++) { selection.ProcessInput(true, false, true); yield return null; }
            Check(!selection.Ready, "Held launch key on refocus cannot launch"); yield return ReleaseMenu(); yield return new WaitForSecondsRealtime(0.2f); Capture("Entry-00-Selection.png");
            var paths = new Dictionary<bool, string>(); var bytes = new Dictionary<bool, string>(); var saved = new Dictionary<bool, string>();
            for (int round = 0; round < 4; round++)
            {
                bool chart = round == 1 || round == 2; bool pointer = round == 0 || round == 2; string label = chart ? "Chart" : "Resources";
                if (pointer) Click(chart ? "select-chart" : "select-resource"); else selection.ProcessInput(!chart, chart, true);
                CloseCapture(); yield return Game(chart);
                Check(FindFirstObjectByType<PaxExpeditionSelection>() == null && FindObjectsByType<PaxPause>(FindObjectsSortMode.None).Length == 1, label + " launch owns exactly one gameplay pause and no menu owner");
                var world = FindFirstObjectByType<PaxWorld>(); var stocks = FindFirstObjectByType<PaxStockpile>(); var builder = FindFirstObjectByType<PaxBuilder>(); var mission = FindFirstObjectByType<PaxExpedition>(); var player = FindFirstObjectByType<PaxPlayer>();
                var cells = FindFirstObjectByType<PaxResourceCache>().Cells; string id = chart ? "timber" : "stone";
                Check(world.Contains(cells[0]) && world.Contains(cells[1]) && stocks.Count(id) == 0 && mission.SurveyedCount == 0, label + " launch starts a fresh authored world, zero stocks and empty journal");
                if (!paths.ContainsKey(chart))
                {
                    Check(builder.Mine(cells[0]), label + " harvest uses existing finite builder accounting"); input.ApplyFrame(new PaxInputFrame { save = true });
                    paths[chart] = save.SavePath; bytes[chart] = File.ReadAllText(save.SavePath); saved[chart] = stocks.Capture().Encode(); Check(!File.Exists(save.BackupPath), label + " explicit F5 writes only its disposable current slot");
                }
                else
                {
                    Check(save.SavePath == paths[chart] && bytes[chart] == File.ReadAllText(save.SavePath), label + " reentry retains exact saved bytes without auto-loading");
                    input.ApplyFrame(new PaxInputFrame { load = true }); Check(saved[chart] == stocks.Capture().Encode() && !world.Contains(cells[0]) && mission.SurveyedCount == 0, label + " explicit F9 alone restores the existing combined snapshot, not the journal");
                }
                Check(builder.Mine(cells[1]) && stocks.Count(id) == 2, label + " unsaved edits are distinct from the retained snapshot");
                player.Warp(mission.Sites[0].transform.position); yield return Frames(35); Check(mission.Sites[0].Surveyed, label + " fixture establishes live session discovery to protect on Cancel");
                if (round == 3) Time.timeScale = 0.75f;
                pause.Pause(); yield return new WaitForSecondsRealtime(0.2f); if (round < 2) Capture("Entry-" + label + "-Paused.png");
                string live = stocks.Capture().Encode(); Vector3 position = player.transform.position; float velocity = player.VerticalSpeed; int surveys = mission.SurveyedCount, revision = world.Revision;
                Click("pause-selection"); Check(pause.ConfirmingReturn, label + " authored return button opens a separate confirmation");
                pause.ConfirmReturn(); Check(!pause.Returning, label + " opening click cannot also confirm discard"); yield return ReleasePause(); yield return new WaitForSecondsRealtime(0.2f);
                Check(document.rootVisualElement.focusController.focusedElement == document.rootVisualElement.Q<Button>("return-cancel"), label + " discard dialog defaults keyboard focus to Cancel");
                if (round < 2) Capture("Entry-" + label + "-Confirm.png");
                Click("return-cancel"); Check(pause.IsPaused && !pause.ConfirmingReturn && Time.timeScale == 0 && position == player.transform.position && velocity == player.VerticalSpeed && live == stocks.Capture().Encode() && surveys == mission.SurveyedCount && revision == world.Revision, label + " actual Cancel button preserves exact live paused state");
                Check(bytes[chart] == File.ReadAllText(paths[chart]) && !File.Exists(paths[chart] + ".bak"), label + " Cancel performs no implicit save/load or backup write");
                yield return ReleasePause(); Click("pause-selection"); yield return ReleasePause();
                pause.ProcessFocus(false); pause.ConfirmReturn(); Check(!pause.Returning && pause.IsPaused, label + " focus loss cannot confirm discard"); pause.ProcessFocus(true);
                pause.ConfirmReturn(); Check(!pause.Returning, label + " confirmation stays gated on focus regain until release"); yield return ReleasePause();
                if (round == 0)
                {
                    pause.ProcessInput(true, true); Check(!pause.ConfirmingReturn && pause.IsPaused, "Fresh Escape cancels the dialog without resuming");
                    for (int i = 0; i < 3; i++) { pause.ProcessInput(true, true); yield return null; } Check(pause.IsPaused, "Holding Escape after Cancel cannot resume or discard");
                    yield return ReleasePause(); Click("pause-selection"); yield return ReleasePause();
                }
                Click("return-confirm"); Check(pause.Returning, label + " explicit authored discard button owns one return transition"); CloseCapture(); yield return WaitScene("PaxExpeditionSelection"); yield return Menu();
                Check(Time.timeScale == (round == 3 ? 0.75f : 1f), label + " return releases the prior pause clock exactly");
                Check(bytes[chart] == File.ReadAllText(paths[chart]) && !File.Exists(paths[chart] + ".bak"), label + " confirmed discard retains saved file bytes and writes no backup");
                Check(FindFirstObjectByType<PaxPause>() == null && FindObjectsByType<PaxExpeditionSelection>(FindObjectsSortMode.None).Length == 1, label + " return leaves one selection owner and no gameplay pause owner");
            }
            Check(paths[false] != paths[true] && bytes[false] == File.ReadAllText(paths[false]) && bytes[true] == File.ReadAllText(paths[true]), "Both independent finite slots remain byte-preserved across all four button/keyboard launches");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Entry-99-Returned.png"); Finish();
        }
        private IEnumerator WaitScene(string name)
        {
            for (int i = 0; i < 240 && SceneManager.GetActiveScene().name != name; i++) yield return null;
            Check(SceneManager.GetActiveScene().name == name, "Scene transition reaches " + name);
        }
        private IEnumerator Menu()
        {
            selection = FindFirstObjectByType<PaxExpeditionSelection>(); selection.UseValidationInput(); yield return ReleaseMenu(); SetupCapture(); yield return Frames(8);
        }
        private IEnumerator Game(bool chart)
        {
            yield return WaitScene(chart ? "PaxLamplighterChart" : "PaxResourceExpedition");
            pause = FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); input = FindFirstObjectByType<PaxInput>(); input.enabled = false; FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            save = FindFirstObjectByType<PaxPlaytestSave>(); save.UseValidationDirectory(slots); yield return Frames(2);
            var player = FindFirstObjectByType<PaxPlayer>(); var world = FindFirstObjectByType<PaxWorld>(); Vector3 position = player.transform.position; int revision = world.Revision;
            for (int i = 0; i < 4; i++) { pause.ProcessInput(false, true); input.ApplyFrame(new PaxInputFrame { movement = Vector2.one, jumpPressed = true, mine = true, place = true }); yield return null; }
            Check(pause.BlocksActions && revision == world.Revision && Vector3.Distance(position, player.transform.position) < 0.08f, "Launch transition consumes held movement/jump/mouse frames without click-through");
            yield return ReleasePause(); yield return Frames(15); SetupCapture(); yield return Frames(8);
        }
        private IEnumerator ReleaseMenu() { for (int i = 0; i < 3; i++) { selection.ProcessInput(false, false, false); yield return null; } }
        private IEnumerator ReleasePause() { for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; } }
        private void Click(string name)
        {
            var button = document.rootVisualElement.Q<Button>(name); var hit = document.rootVisualElement.panel.Pick(button.worldBound.center);
            Check(button.enabledInHierarchy && button.worldBound.width > 100 && (hit == button || button.Contains(hit)), "Authored pointer button is visible and hit-testable: " + name);
            var mouse = new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center };
            using (var down = PointerDownEvent.GetPooled(mouse)) button.SendEvent(down); mouse.type = EventType.MouseUp;
            using (var up = PointerUpEvent.GetPooled(mouse)) button.SendEvent(up);
        }
        private void SetupCapture()
        {
            cameraView = Camera.main; document = FindFirstObjectByType<UIDocument>(); originalPanel = document.panelSettings;
            target = new RenderTexture(1600, 900, 24); target.Create(); cameraView.targetTexture = target;
            panel = Instantiate(originalPanel); panel.targetTexture = target; panel.clearColor = false; document.panelSettings = panel;
        }
        private void LateUpdate() { if (cameraView != null && target != null) cameraView.Render(); }
        private void Capture(string name)
        {
            var previous = RenderTexture.active; var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Destroy(image); }
        }
        private void CloseCapture()
        {
            if (cameraView != null) cameraView.targetTexture = null; if (document != null && originalPanel != null) document.panelSettings = originalPanel;
            if (panel != null) Destroy(panel); if (target != null) { target.Release(); Destroy(target); } cameraView = null; document = null; target = null; panel = null;
        }
        private void Finish()
        {
            CloseCapture(); File.WriteAllLines(Path.Combine(output, "EntryPlayerSmoke.txt"), checks); Debug.Log($"PAX_ENTRY_PLAYER_SMOKE: {checks.Count - failures}/{checks.Count} passed; evidence {output}");
            Time.timeScale = 1; Time.captureDeltaTime = 0; Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
