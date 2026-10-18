using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pax
{
    // Opt-in, owned-path-only four-process proof. Never spawned in normal gameplay.
    [DefaultExecutionOrder(200)]
    public sealed class PaxContinueSmoke : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>(); private int failures;
        private string directory, output, phase, primary, primaryBytes, backupBytes;
        private PaxExpeditionSelection menu; private PaxPause pause; private PaxPlayer player; private PaxCamera rig; private PaxBuilder builder; private PaxWorld world; private PaxStockpile stocks; private PaxPlaytestSave save; private PaxExpedition mission; private PaxChartRoute route; private PaxInput input;
        private Camera view; private UIDocument document; private PanelSettings original, panel; private RenderTexture target;
        private void Check(bool ok, string description) { checks.Add((ok ? "PASS " : "FAIL ") + description); if (!ok) failures++; }
        private static string Arg(string key) { var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); Time.captureDeltaTime = 1f / 60;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../SmokeEvidence"));
            phase = Arg("-paxContinuePhase"); directory = Path.GetFullPath(Arg("-paxValidationDir") ?? output);
            if (!directory.StartsWith(output.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Array.IndexOf(new[] { "seed", "primary", "backup", "blocked" }, phase) < 0)
            { Debug.LogError("Continue probe requires a named phase and owned SmokeEvidence child directory."); Application.Quit(2); yield break; }
            Directory.CreateDirectory(directory); Directory.CreateDirectory(output);
            // Flatten nested fixture enumerators so a failed assertion exits this diagnostic player cleanly.
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                bool more = false; object next = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; } catch (Exception caught) { error = caught; }
                if (error != null) { Check(false, "Fixture aborted: " + error.Message); break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            Finish();
        }
        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(3.5f); yield return Menu();
            var slot = menu.ContinueCatalog.ForScene(menu.EmberScenePath); primary = slot.PathFor(directory);
            if (phase == "seed")
            {
                Check(!menu.CanContinue(1) && !menu.CanContinue(2) && !menu.CanContinue(3), "Missing owned snapshots disable all Continue controls while fresh choices remain available"); Capture("Continue-00-Empty-720.png");
                Click("select-ember"); CloseCapture(); yield return Game(false);
                builder.ToggleCursor(); input.ApplyFrame(new PaxInputFrame { toggleGhostTargets = true });
                foreach (var cell in FindFirstObjectByType<PaxResourceCache>().Cells)
                {
                    rig.SnapToTarget(); var point = rig.View.WorldToScreenPoint(PaxGrid.Centre(cell) + Vector3.up * 0.501f); builder.Select(point);
                    if (builder.CandidateCell != cell) throw new InvalidOperationException("Pointer fixture must target cache " + cell + "; actual=" + builder.CandidateCell + "; mode=" + builder.Mode);
                    builder.SetPointer(point, true, false); yield return Frames(3);
                    Check(!world.Contains(cell), "Real pointer-frame mine earns authored supply " + cell);
                }
                Check(stocks.Count("timber") == 5 && stocks.Count("stone") == 2, "Seed process earns the exact finite cache through mining frames");
                input.ApplyFrame(new PaxInputFrame { save = true }); Check(File.Exists(save.SavePath), "Explicit F5 checkpoints gathered world, stockpile and harvest history");
                yield return Walk(route.Departure.position); yield return Frames(35); Check(mission.Sites[0].Surveyed, "Seed process earns a session field note by actual walking"); yield return Walk(route.NearBank.position); rig.Orbit(180, 0); builder.TrySelectMaterial("timber");
                yield return Build(PaxGrid.Centre(route.Timber[0].Cell - Vector3Int.forward) + Vector3.forward * 0.501f, route.Timber[0]);
                input.ApplyFrame(new PaxInputFrame { save = true });
                Check(stocks.Count("timber") == 4 && stocks.Count("stone") == 2 && route.Timber[0].IsSolid && stocks.Capture().Harvested.Count == 7, "Seed process earns one built crossing, 4/2 remaining units and seven harvested originals");
                File.Copy(save.SavePath, Path.Combine(directory, "ExpectedPrimary.xml"), true); File.Copy(save.BackupPath, Path.Combine(directory, "ExpectedBackup.xml"), true);
                Check(File.ReadAllText(save.SavePath) != File.ReadAllText(save.BackupPath), "Current built snapshot and previous gathered snapshot are distinct");
                yield return new WaitForSecondsRealtime(0.2f); Capture("Continue-01-Seed-Saved-720.png"); Check(true, "Seed process exits now; later processes must read the existing files"); yield break;
            }
            primaryBytes = File.ReadAllText(Path.Combine(directory, "ExpectedPrimary.xml")); backupBytes = File.ReadAllText(Path.Combine(directory, "ExpectedBackup.xml"));
            if (phase == "backup") File.WriteAllText(primary, "owned-corrupt-primary");
            if (phase == "blocked")
            {
                var blocked = PaxResourceSnapshot.Decode(primaryBytes); var camp = new Vector3Int(0, 0, -5); blocked.Tiles.Placed.Add(camp); blocked.Tiles.Materials.Add(camp, "timber"); blocked.Counts["timber"]--;
                File.WriteAllText(primary, blocked.Encode());
            }
            menu.RefreshSaves(); yield return Frames(3); Check(menu.CanContinue(3) && !menu.CanContinue(1) && !menu.CanContinue(2), "Separate process offers only the matching compatible Ember slot");
            if (phase == "primary")
            {
                Check(File.ReadAllText(primary) == primaryBytes && File.ReadAllText(primary + ".bak") == backupBytes, "New process/menu visit preserve exact current and backup bytes"); Capture("Continue-10-Available-720.png");
                Click("select-ember"); CloseCapture(); yield return Game(false);
                Check(stocks.Count("timber") == 0 && stocks.Count("stone") == 0 && world.Contains(FindFirstObjectByType<PaxResourceCache>().Cells[0]) && !route.Timber[0].IsSolid, "Fresh launch ignores existing saved progress and starts the authored world");
                Check(File.ReadAllText(primary) == primaryBytes && File.ReadAllText(primary + ".bak") == backupBytes, "Fresh play never overwrites existing snapshots"); Capture("Continue-11-Fresh-Kept-720.png");
                pause.Pause(); pause.RequestReturn(); yield return Release(); Click("return-confirm"); CloseCapture(); yield return WaitScene("PaxExpeditionSelection"); yield return Menu();
                Check(File.ReadAllText(primary) == primaryBytes && File.ReadAllText(primary + ".bak") == backupBytes, "Confirmed return/menu visit still leave both saved files untouched");
            }
            if (phase == "backup") { Check(menu.ContinueReason(3).Contains("Recoverable previous"), "Menu explicitly labels the compatible backup recovery"); Capture("Continue-20-Backup-Menu-720.png"); }
            if (phase == "blocked") Capture("Continue-30-Spawn-Check-720.png");
            string inputFile = File.ReadAllText(primary); Click("continue-ember"); CloseCapture(); yield return Game(true);
            var receiver = FindFirstObjectByType<PaxContinueReceiver>();
            Check(FindObjectsByType<PaxContinueTransfer>(FindObjectsSortMode.None).Length == 0, "Continue transition owner is cleaned after completion/failure");
            Check(File.ReadAllText(primary) == inputFile && File.ReadAllText(primary + ".bak") == backupBytes, "Continue is read-only for current and backup files");
            if (phase == "blocked")
            {
                Check(!receiver.Restored && stocks.Count("timber") == 0 && stocks.Count("stone") == 0 && !world.Contains(new Vector3Int(0, 0, -5)) && world.Contains(FindFirstObjectByType<PaxResourceCache>().Cells[0]), "Spawn-overlap rejection preserves the entire fresh world/stockpile, without loading the valid backup");
                Check(save.Status.Contains("inside the player") && save.Status.Contains("F9"), "Spawn rejection remains readable with a step-aside/manual-load action"); yield return new WaitForSecondsRealtime(0.2f); Capture("Continue-31-Rejected-720.png");
                yield return Walk(route.Departure.position); input.ApplyFrame(new PaxInputFrame { load = true });
                Check(world.Contains(new Vector3Int(0, 0, -5)) && stocks.Count("timber") == 3 && stocks.Count("stone") == 2 && File.ReadAllText(primary) == inputFile, "Fresh play remains usable: actual walking aside then explicit F9 safely restores the blocked snapshot"); yield return new WaitForSecondsRealtime(0.2f); Capture("Continue-32-Manual-Retry-720.png"); yield break;
            }
            string wanted = phase == "backup" ? backupBytes : primaryBytes;
            Check(receiver.Restored && stocks.Capture().Encode() == PaxResourceSnapshot.Decode(wanted).Encode(), "Separate process restores exact tiles, all material stocks and once-only harvest history");
            Check(mission.SurveyedCount == 0 && Mathf.Abs(player.transform.position.z + 5) < 0.1f, "Continue starts a fresh session journal at the authored camp, not the seed player's saved location");
            Check(save.Status.Contains(phase == "backup" ? "previous compatible" : "Continued current"), "HUD identifies the actual current/previous snapshot restored");
            yield return new WaitForSecondsRealtime(0.2f); Capture(phase == "backup" ? "Continue-21-Backup-Restored-720.png" : "Continue-12-Restored-720.png");
            if (phase == "backup") { Check(File.ReadAllText(primary) == "owned-corrupt-primary", "Backup recovery does not silently repair or replace the corrupt current file"); yield break; }
            var originalCell = FindFirstObjectByType<PaxResourceCache>().Cells[0]; builder.TrySelectMaterial("timber"); bool conserved = true;
            for (int i = 0; i < 3; i++) conserved &= builder.Place(originalCell) && stocks.Count("timber") == 3 && builder.Mine(originalCell) && stocks.Count("timber") == 4;
            Check(conserved && stocks.Capture().Harvested.Count == 7, "Cross-process harvest history survives repeated rebuild/mining without farming");
            builder.ToggleCursor(); rig.Orbit(180, 0); yield return Walk(route.Departure.position); yield return Frames(35); yield return Walk(route.NearBank.position);
            for (int i = 0; i < route.Timber.Length; i++)
            {
                if (i == 2) { yield return Walk(route.Mooring.position); yield return Frames(35); }
                var marker = route.Timber[i]; var direction = i < 2 ? Vector3Int.forward : Vector3Int.right;
                if (!marker.IsSolid) yield return Build(PaxGrid.Centre(marker.Cell - direction) + (Vector3)direction * 0.501f, marker);
                yield return Walk(PaxGrid.Centre(marker.Cell) + Vector3.up * 0.53f); Check(player.Grounded, "Actual continued walk on crossing segment " + (i + 1));
            }
            rig.ResetView(); builder.TrySelectMaterial("stone"); foreach (var marker in route.Stone) yield return Build((Vector3)marker.Cell + new Vector3(0, 0.02f, 0), marker);
            for (int jump = 0; jump < 2; jump++) { var f = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f); player.SetCommand(new Vector2(Vector3.Dot(Vector3.right, r), Vector3.Dot(Vector3.right, f)), true, true); yield return Frames(jump == 0 ? 13 : 34); player.SetCommand(Vector2.zero, false, false); yield return Frames(45); Check(player.Grounded && Mathf.Abs(player.transform.position.y - (jump + 1)) < 0.08f, "Actual continued jump lands on ridge level " + (jump + 1)); }
            yield return Walk(route.Chart.position); yield return Frames(35); Check(mission.SurveyedCount == 3 && mission.RepairCount == 7 && mission.TryActivate() && stocks.Count("timber") == 0 && stocks.Count("stone") == 0, "Continued partial world can be physically completed with pointer repairs, two jumps and newly earned field notes, without Warp");
            Check(File.ReadAllText(primary) == primaryBytes && File.ReadAllText(primary + ".bak") == backupBytes, "Completing continued play does not autosave or overwrite the original partial checkpoint");
            builder.SetPointer(new Vector2(-10000, -10000), false, false); rig.Zoom(-3); yield return new WaitForSecondsRealtime(0.2f); Capture("Continue-13-Completed-720.png");
        }
        private IEnumerator Menu()
        { menu = FindFirstObjectByType<PaxExpeditionSelection>(); menu.UseValidationInput(); menu.UseValidationDirectory(directory); for (int i = 0; i < 3; i++) { menu.ProcessInput(false, false, false, false, false, false, false); yield return null; } menu.RefreshSaves(); SetupCapture(); yield return Frames(10); foreach (string key in new[] { "resource", "chart", "ember" }) foreach (string prefix in new[] { "select-", "continue-" }) { var rect = document.rootVisualElement.Q<Button>(prefix + key).worldBound; Check(rect.width > 100 && rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= 1280 && rect.yMax <= 720, "Authored menu control fits 1280x720: " + prefix + key); } }
        private IEnumerator Game(bool continuation)
        {
            yield return WaitScene("PaxEmberReach"); pause = FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); input = FindFirstObjectByType<PaxInput>(); input.enabled = false; FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            player = FindFirstObjectByType<PaxPlayer>(); rig = FindFirstObjectByType<PaxCamera>(); builder = FindFirstObjectByType<PaxBuilder>(); world = FindFirstObjectByType<PaxWorld>(); stocks = FindFirstObjectByType<PaxStockpile>(); save = FindFirstObjectByType<PaxPlaytestSave>(); save.UseValidationDirectory(directory); mission = FindFirstObjectByType<PaxExpedition>(); route = FindFirstObjectByType<PaxChartRoute>();
            if (continuation) { float until = Time.realtimeSinceStartup + 20; var receiver = FindFirstObjectByType<PaxContinueReceiver>(); while (!receiver.Finished && Time.realtimeSinceStartup < until) yield return null; if (!receiver.Finished) throw new InvalidOperationException("Continue did not finish within diagnostic deadline."); }
            yield return Release(); yield return Frames(15); SetupCapture(); yield return Frames(10);
        }
        private IEnumerator Release() { for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; } }
        private static IEnumerator WaitScene(string name) { float until = Time.realtimeSinceStartup + 20; while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < until) yield return null; if (SceneManager.GetActiveScene().name != name) throw new InvalidOperationException("Scene load deadline: " + name); }
        private IEnumerator Walk(Vector3 targetPosition)
        { for (int i = 0; i < 240; i++) { var delta = targetPosition - player.transform.position; delta.y = 0; if (delta.magnitude < 0.09f) { player.SetCommand(Vector2.zero, false, false); yield break; } var f = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f); player.SetCommand(new Vector2(Vector3.Dot(delta.normalized, r), Vector3.Dot(delta.normalized, f)), false, false); yield return null; } player.SetCommand(Vector2.zero, false, false); throw new InvalidOperationException("Physical continuation walk blocked toward " + targetPosition); }
        private IEnumerator Build(Vector3 face, PaxRepairMarker marker) { rig.SnapToTarget(); builder.SetPointer(rig.View.WorldToScreenPoint(face), false, true); yield return Frames(3); Check(marker.IsSolid, "Real pointer-frame construction at " + marker.Cell); if (!marker.IsSolid) throw new InvalidOperationException(builder.LastAction); }
        private void Click(string name)
        { var button = document.rootVisualElement.Q<Button>(name); var hit = document.rootVisualElement.panel.Pick(button.worldBound.center); Check(button.enabledInHierarchy && (hit == button || button.Contains(hit)), "Actual authored pointer target: " + name); var mouse = new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center }; using (var down = PointerDownEvent.GetPooled(mouse)) button.SendEvent(down); mouse.type = EventType.MouseUp; using (var up = PointerUpEvent.GetPooled(mouse)) button.SendEvent(up); }
        private void SetupCapture()
        { view = Camera.main; document = FindFirstObjectByType<UIDocument>(); original = document.panelSettings; target = new RenderTexture(1280, 720, 24); target.Create(); view.targetTexture = target; panel = Instantiate(original); panel.targetTexture = target; panel.clearColor = false; document.panelSettings = panel; }
        private void LateUpdate() { if (view != null && target != null) view.Render(); }
        private void Capture(string name) { var previous = RenderTexture.active; var bitmap = new Texture2D(1280, 720, TextureFormat.RGB24, false); try { RenderTexture.active = target; bitmap.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); bitmap.Apply(); File.WriteAllBytes(Path.Combine(output, name), bitmap.EncodeToPNG()); } finally { RenderTexture.active = previous; Destroy(bitmap); } }
        private void CloseCapture() { if (view != null) view.targetTexture = null; if (document != null && original != null) document.panelSettings = original; if (panel != null) Destroy(panel); if (target != null) { target.Release(); Destroy(target); } view = null; document = null; target = null; panel = null; }
        private void Finish() { CloseCapture(); File.WriteAllLines(Path.Combine(output, "Continue-" + phase + "-Checks.txt"), checks); Debug.Log("PAX_CONTINUE_SMOKE " + phase + ": " + (checks.Count - failures) + "/" + checks.Count + " passed; owned directory " + directory); Time.timeScale = 1; Time.captureDeltaTime = 0; Application.Quit(failures == 0 ? 0 : 1); }
    }
}
