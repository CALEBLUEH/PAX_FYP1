using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    // Explicitly opt-in validation of the built player. Never active in ordinary gameplay.
    [DefaultExecutionOrder(200)]
    public sealed class PaxPlayerSmoke : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private int failures;
        private string output;
        private RenderTexture captureTarget;
        private Camera captureCamera;
        private UIDocument captureDocument;
        private PanelSettings originalPanel, capturePanel;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginIfRequested()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-paxSmoke") < 0) return;
            Application.runInBackground=true;
            new GameObject("PAX opt-in player smoke probe").AddComponent<PaxPlayerSmoke>();
        }

        private void Check(bool passed, string description)
        {
            checks.Add((passed ? "PASS " : "FAIL ") + description);
            if (!passed) failures++;
        }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath,"../SmokeEvidence"));
            Directory.CreateDirectory(output);
            // Let the standalone splash and UI initialization finish before full-frame evidence.
            yield return new WaitForSecondsRealtime(3.5f);
            Time.captureDeltaTime=1f/60;
            for(int i=0;i<15;i++) yield return null;
            // Discovery here is diagnostic-only; gameplay references are all scene assigned.
            var player=FindFirstObjectByType<PaxPlayer>();
            var input=FindFirstObjectByType<PaxInput>();
            var world=FindFirstObjectByType<PaxWorld>();
            var builder=FindFirstObjectByType<PaxBuilder>();
            var cameraRig=FindFirstObjectByType<PaxCamera>();
            var cutaway=FindFirstObjectByType<PaxCutaway>();
            captureDocument=FindFirstObjectByType<UIDocument>();
            Check(player!=null && input!=null && world!=null && builder!=null && cameraRig!=null && cutaway!=null && captureDocument!=null,"Scene components loaded");
            if(failures>0) { Finish(); yield break; }
            input.enabled=false;
            // Hidden Windows swap chains can capture black. Render the actual scene camera and
            // authored UI panel to the same offscreen target, without editing the scene assets.
            captureTarget=new RenderTexture(1600,900,24);
            captureTarget.Create();
            captureCamera=cameraRig.View;
            captureCamera.targetTexture=captureTarget;
            originalPanel=captureDocument.panelSettings;
            capturePanel=Instantiate(originalPanel);
            capturePanel.targetTexture=captureTarget;
            capturePanel.clearColor=false;
            captureDocument.panelSettings=capturePanel;
            for(int i=0;i<20;i++) yield return null;
            var status=captureDocument.rootVisualElement.Q<Label>("status");
            Check(status!=null && !string.IsNullOrWhiteSpace(status.text) && status.worldBound.width>50,"Authored HUD laid out and status updated");
            Check(player.Grounded,"Player starts grounded");
            var materialSelection = FindFirstObjectByType<PaxMaterialSelection>();
            if (materialSelection != null)
            {
                FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
                yield return RunPalette(materialSelection, player, world, builder, cameraRig, cutaway);
                Finish(); yield break;
            }
            var expedition = FindFirstObjectByType<PaxExpedition>();
            if (expedition != null)
            {
                FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
                yield return RunExpedition(expedition, player, world, builder, cameraRig, cutaway);
                Finish();
                yield break;
            }
            var generated = FindFirstObjectByType<PaxGeneratedIsland>();
            if (generated != null)
            {
                yield return RunGeneratedIsland(generated, player, world, builder, cameraRig, cutaway);
                Finish();
                yield break;
            }
            Capture("01-Open-field-HUD.png");
            builder.ToggleCursor();
            builder.SetPointer(cameraRig.View.WorldToScreenPoint(PaxGrid.Centre(new Vector3Int(-6,3,9))),false,false);
            for(int i=0;i<15;i++) yield return null;
            Check(builder.MiningBlock==PaxActionBlock.OutOfReach && builder.MiningUsesInvalidMaterial,"Out-of-range target retains an invalid preview and specific reason");
            Capture("00-Out-of-range-HUD.png");
            builder.ToggleCursor();
            builder.SetPointer(cameraRig.View.WorldToScreenPoint(PaxGrid.Centre(new Vector3Int(-3,1,-3))),false,false);
            Vector3 before=player.transform.position;
            player.SetCommand(Vector2.up,false,false);
            for(int i=0;i<30;i++) yield return null;
            player.SetCommand(Vector2.zero,false,false);
            Check(Vector3.Distance(before,player.transform.position)>1.9f,"Movement in built player");
            player.Warp(new Vector3(0,0.03f,-3));
            for(int i=0;i<10;i++) yield return null;
            float initial=player.transform.position.y, apex=initial;
            player.SetCommand(Vector2.zero,true,true);
            for(int i=0;i<65;i++) { yield return null; apex=Mathf.Max(apex,player.transform.position.y); }
            player.SetCommand(Vector2.zero,false,false);
            Check(apex-initial>1.85f && apex-initial<2.05f,"Held jump reaches two tiles");
            Check(player.Grounded,"Jump lands on solid floor");
            Check(builder.Place(new Vector3Int(-2,0,-3)),"Supported placement in built player");
            Check(builder.Mine(new Vector3Int(-2,0,-3)),"Mining updates built player world");
            Check(!builder.Place(new Vector3Int(0,0,-3)),"Player-overlap placement rejected");
            Check(!builder.Mine(new Vector3Int(10,0,10)),"Out-of-reach mining rejected");
            var saveSession=FindFirstObjectByType<PaxPlaytestSave>();
            Check(saveSession!=null,"Authored playtest save component loaded");
            if(saveSession!=null)
            {
                string saveDirectory=Path.Combine(output,"SaveSlot-"+Guid.NewGuid().ToString("N"));
                saveSession.UseValidationDirectory(saveDirectory);
                var savedCell=new Vector3Int(-2,0,-3);
                bool savedA=builder.Place(savedCell) && saveSession.SaveEdits();
                bool savedB=builder.Mine(savedCell) && saveSession.SaveEdits();
                Check(savedA && savedB && File.Exists(saveSession.BackupPath),"Local save and previous-save backup written");
                File.WriteAllText(saveSession.SavePath,"<corrupt");
                int beforeLoad=world.Revision;
                Check(!saveSession.LoadEdits() && world.Revision==beforeLoad,"Malformed save rejected without world mutation");
                Check(saveSession.LoadEdits(true) && world.Contains(savedCell),"Previous save recovers edited tile in built player");
                Check(saveSession.SaveEdits() && Directory.GetFiles(saveDirectory,"*.invalid-*").Length==1,"Corrupt primary preserved while saving recovered edits");
                for(int i=0;i<15;i++) yield return null;
                Capture("07-Recovered-save-HUD.png");
                builder.Mine(savedCell);
            }
            int index=2;
            foreach(var position in new[]{new Vector3(-6,0.03f,6),new Vector3(7,0.03f,7),new Vector3(6,0.03f,-7),new Vector3(0,0.03f,-7),new Vector3(0,-2.97f,7)})
            {
                player.Warp(position); cameraRig.SnapToTarget();
                for(int i=0;i<55;i++) yield return null;
                cutaway.RefreshNow();
                bool solid=true, precedence=true;
                foreach(var block in world.Blocks)
                {
                    solid &= block.Solid.enabled;
                    solid &= block.SolidShadow!=null && block.SolidShadow.enabled &&
                        block.SolidShadow.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    if(cutaway.IsObstruction(block)) precedence &= cutaway.TargetFor(block)<=0.04f;
                }
                Check(solid && precedence,$"Solid collision and obstruction precedence at {position}");
                Capture($"0{index++}-Context.png");
                for(int i=0;i<4;i++) yield return null;
            }
            Finish();
        }
        private static IEnumerator WaitFrames(int count) { for (int i = 0; i < count; i++) yield return null; }

        private IEnumerator RunPalette(PaxMaterialSelection selection, PaxPlayer player, PaxWorld world, PaxBuilder builder, PaxCamera cameraRig, PaxCutaway cutaway)
        {
            var cells = new[] { new Vector3Int(-2,0,-3), new Vector3Int(2,0,-3), new Vector3Int(-1,0,-3), new Vector3Int(1,0,-3) };
            string[] ids = { "timber", "stone", "earth", "roof" };
            Check(selection.Palette.Validate(out _) && builder.SelectedMaterialId == "default", "Editable palette starts with legacy-compatible default material");
            selection.SelectNumber(2); builder.ToggleCursor();
            builder.SetPointer(cameraRig.View.WorldToScreenPoint(new Vector3(2, 0.02f, -3)), false, false);
            yield return WaitFrames(5);
            Check(builder.CanPlace && builder.PlacementUsesValidMaterial && world.TryResolveMaterial("timber", out var previewTimber) && selection.PreviewSample.sharedMaterial == previewTimber, "Selected material sample keeps the green valid placement outline");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Palette-00-Valid-Preview-HUD.png");
            builder.SetPointer(cameraRig.View.WorldToScreenPoint(player.transform.position + Vector3.up * 0.02f), false, false);
            yield return WaitFrames(5);
            Check(!builder.CanPlace && !builder.PlacementUsesValidMaterial, "Selected material sample keeps the red blocked placement outline");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Palette-00-Blocked-Preview-HUD.png");
            builder.ToggleCursor();
            for (int i = 0; i < cells.Length; i++)
                Check(selection.SelectNumber(i + 2) && builder.Place(cells[i]), "Number selection and validated placement of " + ids[i]);
            yield return WaitFrames(5);
            string selected = builder.SelectedMaterialId; int revision = world.Revision;
            Check(!selection.SelectNumber(9) && !builder.TrySelectMaterial("unregistered") && builder.SelectedMaterialId == selected && world.Revision == revision, "Invalid slot/ID retains selected material and world without partial action");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Palette-01-Mixed-HUD.png");
            var save = FindFirstObjectByType<PaxPlaytestSave>();
            save.UseValidationDirectory(Path.Combine(output, "PaletteSave-" + Guid.NewGuid().ToString("N")));
            Check(save.SaveEdits() && PaxSaveFile.Read(save.SavePath).Version == 2, "Windows save writes version 2 stable material IDs");
            Check(builder.Mine(cells[2]) && selection.SelectNumber(2) && builder.Place(cells[2]) && save.SaveEdits(), "Mine/replace with a different material writes a current and previous save");
            Check(save.LoadEdits(true), "Previous mixed-material save loads");
            bool exact = true;
            for (int i = 0; i < cells.Length; i++) exact &= world.TryGet(cells[i], out var block) && world.TryResolveMaterial(ids[i], out var material) && block.SurfaceMaterial == material && block.Solid.enabled && block.SolidShadow != null;
            Check(exact, "Every previous tile restores exact material, collider and independent shadow");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Palette-02-Restored-HUD.png");
            Check(save.LoadEdits() && world.TryGet(cells[2], out var current) && world.TryResolveMaterial("timber", out var timber) && current.SurfaceMaterial == timber, "Current save restores the replacement material");
            var invalid = world.CaptureEdits(); invalid.Materials[cells[0]] = "unregistered"; revision = world.Revision;
            Check(!world.TryApplyEdits(invalid, player.BodyBounds, out _) && world.Revision == revision, "Unknown saved material rejects all mutations");
            string backup = File.ReadAllText(save.BackupPath), primary = File.ReadAllText(save.SavePath);
            File.WriteAllText(save.SavePath, primary.Replace("material=\"timber\"", "material=\"unregistered\""));
            Check(!save.LoadEdits() && world.Revision == revision && save.SaveEdits() && backup == File.ReadAllText(save.BackupPath) && Directory.GetFiles(Path.GetDirectoryName(save.SavePath), "*.invalid-*").Length == 1, "Incompatible primary is preserved without overwriting the valid backup");
            foreach (var cell in cells) world.TryMine(cell);
            Physics.SyncTransforms(); yield return WaitFrames(2);
            selection.SelectNumber(3);
            yield return RunExpedition(FindFirstObjectByType<PaxExpedition>(), player, world, builder, cameraRig, cutaway, "pax-palette-");
        }

        private IEnumerator RunExpedition(PaxExpedition mission, PaxPlayer player, PaxWorld world, PaxBuilder builder, PaxCamera cameraRig, PaxCutaway cutaway, string slotPrefix = "pax-expedition-")
        {
            var objective = captureDocument.rootVisualElement.Q<Label>("expedition-objective");
            Check(objective != null && objective.worldBound.width > 200 && mission.Stage == PaxExpeditionStage.Survey && mission.RepairCount == 0, "Authored expedition HUD starts with clear survey and six empty repair cells");
            Capture("Expedition-01-Start-HUD.png");
            cameraRig.enabled = false; cameraRig.transform.position = mission.Sites[0].transform.position;
            yield return WaitFrames(40);
            Check(mission.SurveyedCount == 0, "Camera position alone cannot survey a shelter");
            cameraRig.enabled = true;
            for (int index = mission.Sites.Length - 1; index >= 0; index--)
            {
                var site = mission.Sites[index];
                player.Warp(site.transform.position); cameraRig.SnapToTarget();
                yield return WaitFrames(45); cutaway.RefreshNow();
                bool solids = true, precedence = true;
                foreach (var block in world.Blocks)
                {
                    solids &= block.Solid.enabled && block.SolidShadow != null;
                    if (cutaway.IsObstruction(block)) precedence &= cutaway.TargetFor(block) <= 0.04f;
                }
                Check(site.Surveyed && solids && precedence, site.Title + " real player visit registers with solid/cutaway invariants");
                if (site.Title == "Cave") { yield return new WaitForSecondsRealtime(0.2f); Capture("Expedition-02-Cave-HUD.png"); }
            }
            yield return WaitFrames(30);
            Check(mission.SurveyedCount == 3 && mission.Stage == PaxExpeditionStage.Repair && !mission.TryActivate(), "Out-of-order and repeated visits produce one survey per site, without premature completion");
            player.Warp(new Vector3(1, 0.03f, -3)); cameraRig.SnapToTarget(); yield return WaitFrames(10);
            Check(builder.Place(new Vector3Int(-2, 0, -3)) && mission.RepairCount == 0, "Unrelated placement does not repair the beacon");
            var ordered = new List<PaxRepairMarker>(mission.Repairs);
            ordered.Sort((a, b) => { int c = a.Cell.z.CompareTo(b.Cell.z); return c != 0 ? c : a.Cell.x.CompareTo(b.Cell.x); });
            foreach (var marker in ordered) Check(builder.Place(marker.Cell), "Validated placement at required repair cell " + marker.Cell);
            yield return WaitFrames(5);
            bool blueprintsHidden = true;
            foreach (var marker in mission.Repairs) blueprintsHidden &= !marker.GetComponent<Renderer>().enabled;
            Check(mission.RepairCount == 6 && blueprintsHidden && mission.Stage == PaxExpeditionStage.Return, "Only actual solid repairs count and their blueprint visuals retire");
            Check(!mission.TryActivate(), "Repaired platform cannot remotely activate the beacon");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Expedition-03-Repaired-HUD.png");
            Vector3 forward = Vector3.ProjectOnPlane(cameraRig.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            player.SetCommand(new Vector2(Vector3.Dot(Vector3.back, right), Vector3.Dot(Vector3.back, forward)), true, true);
            yield return WaitFrames(30); player.SetCommand(Vector2.zero, false, false); yield return WaitFrames(35);
            Check(player.Grounded && Mathf.Abs(player.transform.position.y - 1) < 0.06f && mission.AtBeacon, "Real held jump lands on the repaired one-tile platform");
            Check(mission.TryActivate() && mission.Stage == PaxExpeditionStage.Complete, "Survey, construction, player return and activation complete the expedition");
            int revision = world.Revision;
            Check(mission.TryActivate() && world.Revision == revision, "Repeated activation is idempotent and leaves sandbox edits available");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Expedition-04-Complete-HUD.png");
            var save = FindFirstObjectByType<PaxPlaytestSave>();
            save.UseValidationDirectory(Path.Combine(output, "SaveSlot-" + Guid.NewGuid().ToString("N")));
            Check(Path.GetFileName(save.SavePath).StartsWith(slotPrefix, StringComparison.Ordinal) && save.SaveEdits(), "Expedition writes tile state to an independent disposable validation slot");
            player.Warp(new Vector3(1, 0.03f, -3)); yield return WaitFrames(10);
            Check(builder.Mine(new Vector3Int(1, 0, -5)), "Required repair can be mined after completion");
            yield return WaitFrames(2);
            Check(mission.Stage == PaxExpeditionStage.Repair && mission.RepairCount == 5 && !mission.TryActivate() && save.SaveEdits(), "Removing a repair revokes completion; incomplete construction saves normally");
            Check(save.LoadEdits(true), "Previous tile save loads the complete platform"); yield return WaitFrames(2);
            Check(mission.RepairCount == 6 && mission.Stage == PaxExpeditionStage.Return, "Loaded repairs are inferred and require fresh beacon activation");
            Check(save.LoadEdits(), "Current tile save loads the incomplete platform"); yield return WaitFrames(2);
            Check(mission.RepairCount == 5 && mission.Stage == PaxExpeditionStage.Repair, "Loading missing repair tiles immediately updates objective state");
            string savedBytes = File.ReadAllText(save.SavePath); revision = world.Revision;
            mission.RestartSurvey();
            Check(mission.SurveyedCount == 0 && mission.RepairCount == 5 && world.Revision == revision && savedBytes == File.ReadAllText(save.SavePath), "Replay resets session journal while keeping construction and save bytes");
            Check(save.LoadEdits(true), "Replay can reload retained construction"); yield return WaitFrames(2);
            Check(mission.RepairCount == 6 && mission.SurveyedCount == 0 && mission.Stage == PaxExpeditionStage.Survey, "Loading repairs never restores session-only shelter visits");
            cameraRig.SnapToTarget(); yield return new WaitForSecondsRealtime(0.2f); Capture("Expedition-05-Replay-HUD.png");
        }

        private IEnumerator RunGeneratedIsland(PaxGeneratedIsland island, PaxPlayer player, PaxWorld world, PaxBuilder builder, PaxCamera cameraRig, PaxCutaway cutaway)
        {
            Check(world.Blocks.Count == island.AuthoredTileCount && world.CaptureEdits().Placed.Count == 0 && world.CaptureEdits().Removed.Count == 0, "Saved generated baseline loaded without runtime regeneration");
            Capture("Generation-Spawn-HUD.png");
            cameraRig.Zoom(-7);
            for (int i = 0; i < 10; i++) yield return null;
            Capture("Generation-Overview-HUD.png");
            cameraRig.ResetView();
            Vector3 start = player.transform.position;
            player.SetCommand(Vector2.up, false, false);
            for (int i = 0; i < 25; i++) yield return null;
            player.SetCommand(Vector2.zero, false, false);
            Check(Vector3.Distance(start, player.transform.position) > 1.4f, "Generated spawn supports camera-relative movement");
            player.Warp(island.SpawnMarker.position);
            for (int i = 0; i < 15; i++) yield return null;
            var edit = new Vector3Int(-2, 0, -3);
            var terrain = new Vector3Int(1, -1, -3); // Nearest floor face is visible from spawn.
            Check(builder.Place(edit), "Generated world supports validated placement");
            var save = FindFirstObjectByType<PaxPlaytestSave>();
            save.UseValidationDirectory(Path.Combine(output, "SaveSlot-" + Guid.NewGuid().ToString("N")));
            Check(Path.GetFileName(save.SavePath).StartsWith("pax-island-", StringComparison.Ordinal), "Generated scene has a separate authored save slot");
            Check(save.SaveEdits() && builder.Mine(edit) && save.LoadEdits() && world.Contains(edit), "Generated tile edits persist and reload in Windows player");
            Check(builder.Mine(terrain) && !world.Contains(terrain), "Generated terrain can be mined downward");
            Check(save.LoadEdits() && world.TryGet(terrain, out var restored) && restored.Solid.enabled, "Loading restores authored terrain material and collision");
            var incompatible = world.CaptureEdits(); incompatible.World = new string('0', 64);
            int revision = world.Revision;
            Check(!world.TryApplyEdits(incompatible, player.BodyBounds, out _) && revision == world.Revision, "Different-world save rejected without mutation");
            builder.Mine(edit);
            foreach (var structure in island.Structures)
            {
                player.Warp((Vector3)structure.door + new Vector3(0, 0.03f, -1));
                player.enabled = false;
                var body = player.GetComponent<CharacterController>();
                for (int i = 0; i < 45; i++) { body.Move(new Vector3(0, -2f / 60, 4.5f / 60)); yield return null; }
                Check(player.transform.position.z > structure.door.z + 1, structure.kind + " two-high doorway is traversable");
                for (int i = 0; i < 55; i++) { body.Move(new Vector3(0, -2f / 60, 4.5f / 60)); yield return null; }
                Check(player.transform.position.z < structure.centre.z + 1.1f, structure.kind + " wall retains physical collision");
                player.enabled = true;
                player.Warp((Vector3)structure.centre + Vector3.up * 0.03f); cameraRig.SnapToTarget();
                for (int i = 0; i < 45; i++) yield return null;
                cutaway.RefreshNow();
                bool solid = true, precedence = true;
                foreach (var block in world.Blocks)
                {
                    solid &= block.Solid.enabled && block.SolidShadow != null && block.SolidShadow.enabled;
                    if (cutaway.IsObstruction(block)) precedence &= cutaway.TargetFor(block) <= 0.04f;
                }
                Check(solid && precedence && (structure.kind == "Ruin" || cutaway.RoofCoverage > 0), structure.kind + " roof/cutaway preserves collision and obstruction priority");
                yield return new WaitForSecondsRealtime(0.2f);
                Capture("Generation-" + structure.kind + "-HUD.png");
            }
        }

        private void Capture(string file)
        {
            var previous=RenderTexture.active;
            var texture=new Texture2D(captureTarget.width,captureTarget.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=captureTarget;
                texture.ReadPixels(new Rect(0,0,captureTarget.width,captureTarget.height),0,0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(output,file),texture.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; Destroy(texture); }
        }
        private void LateUpdate()
        {
            // Windows can skip automatic camera rendering while its swap chain is hidden.
            // The UI panel renders after this explicit camera draw into the shared target.
            if(captureCamera!=null && captureTarget!=null) captureCamera.Render();
        }
        private void Finish()
        {
            File.WriteAllLines(Path.Combine(output,"PlayerSmoke.txt"),checks);
            Debug.Log($"PAX_PLAYER_SMOKE: {checks.Count-failures}/{checks.Count} passed; evidence {output}");
            Time.captureDeltaTime=0;
            if(captureCamera!=null) captureCamera.targetTexture=null;
            if(captureDocument!=null && originalPanel!=null) captureDocument.panelSettings=originalPanel;
            if(capturePanel!=null) Destroy(capturePanel);
            if(captureTarget!=null) { captureTarget.Release(); Destroy(captureTarget); }
            Application.Quit(failures==0?0:1);
        }
    }
}
