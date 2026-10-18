using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-paxContinueSmoke") >= 0)
            { new GameObject("PAX opt-in Continue process probe").AddComponent<PaxContinueSmoke>(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-paxEntrySmoke") >= 0)
            { new GameObject("PAX opt-in entry smoke probe").AddComponent<PaxEntrySmoke>(); return; }
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-paxReviewEmber") >= 0)
            {
                DontDestroyOnLoad(gameObject); Time.captureDeltaTime = 1f / 60; var menu = FindFirstObjectByType<PaxExpeditionSelection>(); menu.UseValidationInput();
                for (int i = 0; i < 3; i++) { menu.ProcessInput(false, false, false, false); yield return null; }
                captureCamera = Camera.main; captureDocument = FindFirstObjectByType<UIDocument>(); captureTarget = new RenderTexture(1600, 900, 24); captureTarget.Create(); captureCamera.targetTexture = captureTarget;
                originalPanel = captureDocument.panelSettings; capturePanel = Instantiate(originalPanel); capturePanel.targetTexture = captureTarget; capturePanel.clearColor = false; captureDocument.panelSettings = capturePanel;
                yield return new WaitForSecondsRealtime(3.5f); yield return WaitFrames(15); Capture("Ember-00-Selection.png");
                var button = captureDocument.rootVisualElement.Q<Button>("select-ember"); var hit = captureDocument.rootVisualElement.panel.Pick(button.worldBound.center);
                Check(button.enabledInHierarchy && (hit == button || button.Contains(hit)), "Third authored Ember card is available and hit-testable");
                var mouse = new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center };
                using (var down = PointerDownEvent.GetPooled(mouse)) button.SendEvent(down); mouse.type = EventType.MouseUp; using (var up = PointerUpEvent.GetPooled(mouse)) button.SendEvent(up);
                Check(menu.Loading, "Actual third-card pointer events own the Ember launch"); CloseCapture();
                float loadStarted = Time.realtimeSinceStartup, deadline = loadStarted + 20;
                while (SceneManager.GetActiveScene().name != "PaxEmberReach" && Time.realtimeSinceStartup < deadline) yield return null;
                Check(SceneManager.GetActiveScene().name == "PaxEmberReach", "Selection launches the distinct Ember scene; actual=" + SceneManager.GetActiveScene().name + "; seconds=" + (Time.realtimeSinceStartup - loadStarted).ToString("F3")); if (failures > 0) { Finish(); yield break; }
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-paxReviewChart") >= 0)
            { DontDestroyOnLoad(gameObject); yield return SceneManager.LoadSceneAsync("PaxLamplighterChart"); }
            var pause = FindFirstObjectByType<PaxPause>();
            if (pause != null)
            {
                pause.UseValidationInput();
                for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; }
            }
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
            if (pause != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-paxPauseSmoke") >= 0)
                yield return RunPause(pause, input, player, world, builder, cameraRig);
            if (FindFirstObjectByType<PaxCompletionLamps>() != null)
            { FindFirstObjectByType<PaxExpeditionInput>().enabled = false; yield return RunEmber(player, world, builder, cameraRig, cutaway); Finish(); yield break; }
            var chartRoute=FindFirstObjectByType<PaxChartRoute>();
            if(chartRoute!=null)
            {FindFirstObjectByType<PaxExpeditionInput>().enabled=false;yield return RunChart(chartRoute,player,world,builder,cameraRig,cutaway);Finish();yield break;}
            var resources = FindFirstObjectByType<PaxStockpile>();
            if (resources != null)
            {
                FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
                yield return RunResources(resources, player, world, builder, cameraRig, cutaway);
                Finish(); yield break;
            }
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
        private IEnumerator RunPause(PaxPause pause, PaxInput input, PaxPlayer player, PaxWorld world, PaxBuilder builder, PaxCamera rig)
        {
            var save = FindFirstObjectByType<PaxPlaytestSave>(); var stocks = FindFirstObjectByType<PaxStockpile>(); var mission = FindFirstObjectByType<PaxExpedition>();
            FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            save.UseValidationDirectory(Path.Combine(output, "PauseSave-" + Guid.NewGuid().ToString("N")));
            Check(save.SaveEdits(), "Pause fixture checkpoints the empty finite world in a disposable slot");
            string bytes = File.ReadAllText(save.SavePath), accounting = stocks.Capture().Encode(), material = builder.SelectedMaterialId, mode = builder.Mode;
            var cache = FindFirstObjectByType<PaxResourceCache>().Cells[0];
            player.SetCommand(Vector2.zero, true, true); yield return WaitFrames(5);
            Check(player.VerticalSpeed > 0, "Pause entered during a real ascending jump");
            builder.SetPointer(rig.View.WorldToScreenPoint(PaxGrid.Centre(cache)), true, true);
            pause.ProcessInput(true, true);
            Vector3 position = player.transform.position, cameraPosition = rig.transform.position; Quaternion rotation = rig.transform.rotation;
            float velocity = player.VerticalSpeed, zoom = rig.View.orthographicSize, clock = Time.time; int revision = world.Revision, surveys = mission.SurveyedCount;
            input.ApplyFrame(new PaxInputFrame { movement = Vector2.one, jumpPressed = true, jumpHeld = true, mine = true, place = true, orbit = 90, zoom = 5, resetCamera = true, toggleCursor = true, toggleGhostTargets = true, respawn = true, save = true });
            bool rejected = !builder.Mine(cache) && !builder.Place(cache + Vector3Int.up) && !FindFirstObjectByType<PaxMaterialSelection>().SelectNumber(3) && !save.SaveEdits() && !save.LoadEdits() && !save.LoadEdits(true) && !mission.TryActivate();
            mission.RestartSurvey(); rig.Orbit(90, 10); rig.Zoom(3); player.Warp(Vector3.one * 99);
            yield return new WaitForSecondsRealtime(0.25f);
            Check(pause.IsPaused && rejected && revision == world.Revision && surveys == mission.SurveyedCount, "Open overlay blocks gameplay, palette, rescue, activation, journal and save/load actions");
            Check(position == player.transform.position && velocity == player.VerticalSpeed && clock == Time.time && cameraPosition == rig.transform.position && rotation == rig.transform.rotation && zoom == rig.View.orthographicSize, "Pause preserves exact airborne velocity, player/camera transforms, zoom and scaled clock");
            Check(bytes == File.ReadAllText(save.SavePath) && !File.Exists(save.BackupPath) && accounting == stocks.Capture().Encode() && material == builder.SelectedMaterialId && mode == builder.Mode, "Pause keeps snapshot bytes, finite accounting and tool selection unchanged");
            pause.ProcessFocus(false); pause.Resume(); Check(pause.IsPaused, "Focus loss cannot be resumed while unfocused"); pause.ProcessFocus(true); pause.ProcessInput(true, true);
            Check(pause.IsPaused, "Focus regain and held Escape never auto-resume");
            yield return new WaitForSecondsRealtime(0.2f);
            string prefix = FindFirstObjectByType<PaxChartRoute>() != null ? "Chart" : "Resources";
            Capture("Pause-" + prefix + "-Help.png");
            var root = captureDocument.rootVisualElement; var button = root.Q<Button>("pause-resume"); var hit = root.panel.Pick(button.worldBound.center);
            Check(button.worldBound.width > 100 && (hit == button || button.Contains(hit)), "Authored Resume button is visible and hit-testable above the fullscreen overlay");
            var mouse = new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center };
            using (var down = PointerDownEvent.GetPooled(mouse)) button.SendEvent(down);
            mouse.type = EventType.MouseUp;
            using (var up = PointerUpEvent.GetPooled(mouse)) button.SendEvent(up);
            yield return null;
            Check(!pause.IsPaused && pause.BlocksActions, "Actual authored button pointer events resume and retain the release barrier");
            for (int i = 0; i < 5; i++) { pause.ProcessInput(false, true); input.ApplyFrame(new PaxInputFrame { mine = true, place = true, movement = Vector2.one, jumpPressed = true }); yield return null; }
            Check(revision == world.Revision, "Resume click and held mouse/gameplay frames never click through to the world");
            for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; }
            yield return WaitFrames(40);
            Check(!pause.BlocksActions && revision == world.Revision, "Release rearms controls without executing queued pre-pause or overlay actions");
            Check(builder.Mine(cache) && save.LoadEdits() && world.Contains(cache) && stocks.Capture().Encode() == accounting, "Fresh post-release mine works and the disposable snapshot restores exact accounting");
            // Partial discovery dwell must pause rather than complete or reset.
            var start = player.transform.position; player.Warp(mission.Sites[0].transform.position); yield return WaitFrames(5);
            float dwell = mission.Sites[0].DwellSeconds; pause.Pause(); yield return new WaitForSecondsRealtime(0.5f);
            Check(dwell > 0 && !mission.Sites[0].Surveyed && dwell == mission.Sites[0].DwellSeconds, "Discovery dwell remains fractional and unchanged while paused");
            pause.Resume(); for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; } yield return WaitFrames(35);
            Check(mission.Sites[0].Surveyed, "Discovery dwell continues after explicit resume");
            mission.RestartSurvey(); player.Warp(start); yield return WaitFrames(15);
            Time.timeScale = 0.7f; pause.Pause(); pause.enabled = false;
            Check(Time.timeScale == 0.7f, "Disabling pause restores its exact previous non-unit clock");
            Time.timeScale = 1; pause.enabled = true; pause.UseValidationInput();
            pause.ProcessInput(false, false); pause.ProcessInput(true, true); Check(pause.IsPaused, "Reenabled pause accepts a fresh Escape input frame");
            pause.ProcessInput(false, false); pause.ProcessInput(true, true); Check(!pause.IsPaused, "A fresh Escape input frame explicitly resumes");
            for (int i = 0; i < 3; i++) { pause.ProcessInput(false, false); yield return null; }
            Capture("Pause-" + prefix + "-Resumed.png");
        }
        private bool chartRouteFailed;
        private IEnumerator RunEmber(PaxPlayer player, PaxWorld world, PaxBuilder builder, PaxCamera rig, PaxCutaway cutaway)
        {
            var route = FindFirstObjectByType<PaxChartRoute>(); var stocks = FindFirstObjectByType<PaxStockpile>(); var mission = FindFirstObjectByType<PaxExpedition>(); var save = FindFirstObjectByType<PaxPlaytestSave>(); var lamps = FindFirstObjectByType<PaxCompletionLamps>();
            save.UseValidationDirectory(Path.Combine(output, "EmberSave-" + Guid.NewGuid().ToString("N")));
            Check(route.Timber.Length == 5 && route.Stone.Length == 2 && mission.SurveyedCount == 0 && Path.GetFileName(save.SavePath).StartsWith("pax-ember-"), "Ember starts with 5/2 finite route, empty journal and independent slot");
            rig.Zoom(-8); yield return new WaitForSecondsRealtime(0.2f); Capture("Ember-01-Ridge-Overview.png"); rig.ResetView();
            foreach (var cell in FindFirstObjectByType<PaxResourceCache>().Cells) Check(builder.Mine(cell), "Gather reachable Ember supply " + cell);
            Check(stocks.Count("timber") == 5 && stocks.Count("stone") == 2, "Seven authored cache blocks exactly fund the mixed ridge repairs");
            var original = FindFirstObjectByType<PaxResourceCache>().Cells[0]; builder.TrySelectMaterial("timber"); bool conserved = true;
            for (int i = 0; i < 3; i++) conserved &= builder.Place(original) && stocks.Count("timber") == 4 && builder.Mine(original) && stocks.Count("timber") == 5;
            Check(conserved, "Rebuilding and mining the same original cache three times never farms supplies");
            yield return ChartWalk(route.Departure.position, player, rig); if (chartRouteFailed) yield break; yield return WaitFrames(35); Check(mission.Sites[0].Surveyed, "Physical walk reads the Wind ledger");
            yield return ChartWalk(route.NearBank.position, player, rig); if (chartRouteFailed) yield break; builder.ToggleCursor(); rig.Orbit(180, 0); builder.TrySelectMaterial("stone");
            var first = route.Timber[0]; yield return ChartClick(PaxGrid.Centre(first.Cell - Vector3Int.forward) + Vector3.forward * 0.501f, first, builder, rig, false);
            Check(world.Contains(first.Cell) && !first.IsSolid && stocks.Count("stone") == 1 && builder.Mine(first.Cell) && stocks.Count("stone") == 2, "Wrong Stone crossing tile does not count and refunds its actual unit"); builder.TrySelectMaterial("timber");
            for (int i = 0; i < 5; i++)
            {
                if (i == 2) { yield return ChartWalk(route.Mooring.position, player, rig); if (chartRouteFailed) yield break; yield return WaitFrames(35); Check(mission.Sites[1].Surveyed, "North crossing reaches the Leeward cairn and east bank"); builder.SetPointer(new Vector2(-10000, -10000), false, false); yield return new WaitForSecondsRealtime(0.2f); Capture("Ember-02-Cairn-HUD.png"); }
                var marker = route.Timber[i]; var direction = i < 2 ? Vector3Int.forward : Vector3Int.right;
                yield return ChartClick(PaxGrid.Centre(marker.Cell - direction) + (Vector3)direction * 0.501f, marker, builder, rig); if (chartRouteFailed) yield break;
                if (i == 0) Check(save.SaveEdits(), "Checkpoint one Timber crossing tile with exact 4 Timber / 2 Stone stocks");
                yield return ChartWalk(PaxGrid.Centre(marker.Cell) + Vector3.up * 0.53f, player, rig); if (chartRouteFailed) yield break;
                Check(player.Grounded && Mathf.Abs(player.transform.position.y) < 0.08f && stocks.Count("timber") == 4 - i, "Actual grounded walk on Ember crossing segment " + (i + 1));
            }
            rig.ResetView(); builder.TrySelectMaterial("stone"); foreach (var marker in route.Stone) { yield return ChartClick((Vector3)marker.Cell + new Vector3(0, 0.02f, 0), marker, builder, rig); if (chartRouteFailed) yield break; }
            Check(route.StoneCount == 2 && stocks.Count("stone") == 0 && !mission.TryActivate(), "Two reachable matching Stone repairs spend two units; remote completion rejects");
            for (int jump = 0; jump < 2; jump++)
            { var f = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f); player.SetCommand(new Vector2(Vector3.Dot(Vector3.right, r), Vector3.Dot(Vector3.right, f)), true, true); yield return WaitFrames(jump == 0 ? 13 : 34); player.SetCommand(Vector2.zero, false, false); yield return WaitFrames(45); Check(player.Grounded && Mathf.Abs(player.transform.position.y - (jump + 1)) < 0.08f, "Real held jump lands on Ember ridge level " + (jump + 1)); }
            yield return ChartWalk(route.Chart.position, player, rig); if (chartRouteFailed) yield break; yield return WaitFrames(35);
            Check(mission.SurveyedCount == 3 && mission.RepairCount == 7 && mission.TryActivate() && save.SaveEdits(), "Unwarped north/east traversal reaches the keeper and completes with zero rewards"); yield return WaitFrames(3);
            var properties = new MaterialPropertyBlock(); bool green = lamps.Complete; foreach (var lamp in lamps.AdditionalLamps) { lamp.GetPropertyBlock(properties); var color = properties.GetColor("_Color"); green &= color.g > color.r; }
            Check(green && mission.Objective.Contains("home offered forward") && stocks.Count("timber") == 0 && stocks.Count("stone") == 0, "Three-lamp payoff uses mission completion, original field-note text and exact zero stocks");
            cutaway.RefreshNow(); Check(System.Linq.Enumerable.All(world.Blocks, block => block.Solid.enabled && block.SolidShadow != null && (!cutaway.IsObstruction(block) || cutaway.TargetFor(block) <= 0.04f)), "Ridge construction retains collision, logical shadow and obstruction presentation invariants");
            builder.SetPointer(new Vector2(-10000, -10000), false, false); rig.Zoom(-3); yield return new WaitForSecondsRealtime(0.2f); Capture("Ember-03-Three-Lamps-HUD.png");
            Check(save.LoadEdits(true) && stocks.Count("timber") == 4 && stocks.Count("stone") == 2 && mission.RepairCount == 1, "Previous partial-route snapshot restores exact 4/2 stocks and one matching repair"); yield return WaitFrames(3); Check(!lamps.Complete, "Rollback revokes the lamp payoff");
            bool stable = true; for (int i = 0; i < 3; i++) stable &= save.LoadEdits() && stocks.Count("timber") == 0 && stocks.Count("stone") == 0 && mission.RepairCount == 7;
            yield return WaitFrames(3); Check(stable && mission.TryActivate(), "Repeated full loads neither mint resources nor bypass explicit reactivation");
            string bytes = File.ReadAllText(save.SavePath); int revision = world.Revision; mission.RestartSurvey(); yield return WaitFrames(3);
            Check(mission.SurveyedCount == 0 && mission.RepairCount == 7 && world.Revision == revision && !lamps.Complete && bytes == File.ReadAllText(save.SavePath), "F6 field-note replay keeps repairs, zero stocks and snapshot bytes; lamps return dormant"); yield return new WaitForSecondsRealtime(0.2f); Capture("Ember-04-Replay-HUD.png");
        }
        private IEnumerator ChartWalk(Vector3 target,PaxPlayer player,PaxCamera rig)
        {
            for(int frame=0;frame<180;frame++)
            {var delta=target-player.transform.position;delta.y=0;if(delta.magnitude<0.09f){player.SetCommand(Vector2.zero,false,false);yield break;}var forward=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);player.SetCommand(new Vector2(Vector3.Dot(delta.normalized,right),Vector3.Dot(delta.normalized,forward)),false,false);yield return null;}
            player.SetCommand(Vector2.zero,false,false);Check(false,"Physical chart walk blocked toward "+target+" at "+player.transform.position);chartRouteFailed=true;
        }
        private IEnumerator ChartClick(Vector3 surface,PaxRepairMarker marker,PaxBuilder builder,PaxCamera rig,bool expectedMatch=true)
        {rig.SnapToTarget();builder.SetPointer(rig.View.WorldToScreenPoint(surface),false,true);yield return WaitFrames(3);Check(marker.IsSolid==expectedMatch,"Pointer-frame repair material match="+expectedMatch+" at "+marker.Cell);if(marker.IsSolid!=expectedMatch)chartRouteFailed=true;}
        private IEnumerator RunChart(PaxChartRoute route,PaxPlayer player,PaxWorld world,PaxBuilder builder,PaxCamera rig,PaxCutaway cutaway)
        {
            var pile=FindFirstObjectByType<PaxStockpile>();var mission=FindFirstObjectByType<PaxExpedition>();var save=FindFirstObjectByType<PaxPlaytestSave>();var selection=FindFirstObjectByType<PaxMaterialSelection>();
            save.UseValidationDirectory(Path.Combine(output,"ChartSave-"+Guid.NewGuid().ToString("N")));
            Check(pile.Ready&&route.Timber.Length==5&&route.Stone.Length==3&&mission.SurveyedCount==0,"Distinct chart expedition starts with finite mixed-material objective and no discoveries");
            rig.Zoom(-8);yield return new WaitForSecondsRealtime(0.2f);Capture("Chart-00-Camp-Overview.png");rig.ResetView();
            foreach(var cell in FindFirstObjectByType<PaxResourceCache>().Cells)Check(builder.Mine(cell),"Gather authored route supply "+cell);
            Check(pile.Count("timber")==5&&pile.Count("stone")==3&&save.SaveEdits(),"Finite camp supplies and harvested world save as one snapshot");
            yield return ChartWalk(route.Departure.position,player,rig);if(chartRouteFailed)yield break;yield return WaitFrames(40);Check(mission.Sites[0].Surveyed,"Physical walk discovers the departure log");
            yield return ChartWalk(route.NearBank.position,player,rig);if(chartRouteFailed)yield break;builder.ToggleCursor();rig.Orbit(180,0);rig.SnapToTarget();
            var first=route.Timber[0];selection.SelectNumber(3);yield return ChartClick(PaxGrid.Centre(first.Cell-Vector3Int.right)+new Vector3(0.501f,0,0),first,builder,rig,false);
            Check(world.TryGet(first.Cell,out var wrong)&&world.TryResolveMaterial("stone",out var stone)&&wrong.SurfaceMaterial==stone&&route.TimberCount==0&&pile.Count("stone")==2,"Wrong-material solid does not count as a Timber repair");
            Check(save.SaveEdits()&&builder.Mine(first.Cell)&&pile.Count("stone")==3,"Wrong-material repair can be saved then mined for its actual unit");selection.SelectNumber(2);
            yield return ChartClick(PaxGrid.Centre(first.Cell-Vector3Int.right)+new Vector3(0.501f,0,0),first,builder,rig);if(chartRouteFailed)yield break;Check(pile.Count("timber")==4&&save.SaveEdits(),"Corrected Timber repair spends only Timber and checkpoints partial route");
            int built=0;
            foreach(var marker in route.Timber)
            {
                if(!marker.IsSolid)yield return ChartClick(PaxGrid.Centre(marker.Cell-Vector3Int.right)+new Vector3(0.501f,0,0),marker,builder,rig);
                if(chartRouteFailed)yield break;built++;yield return ChartWalk(PaxGrid.Centre(marker.Cell)+Vector3.up*0.53f,player,rig);if(chartRouteFailed)yield break;
                Check(player.Grounded&&Mathf.Abs(player.transform.position.y)<0.08f&&pile.Count("timber")==5-built,"Actual walk on constructed Timber crossing segment "+built);
            }
            yield return ChartWalk(new Vector3(2,0.03f,0),player,rig);yield return ChartWalk(route.Mooring.position,player,rig);if(chartRouteFailed)yield break;yield return WaitFrames(40);
            Check(mission.Sites[1].Surveyed&&route.TimberCount==5,"Crossing reaches the far mooring discovery");builder.SetPointer(new Vector2(-10000,-10000),false,false);yield return new WaitForSecondsRealtime(0.2f);Capture("Chart-01-Crossing-HUD.png");
            yield return ChartWalk(route.Takeoff.position,player,rig);if(chartRouteFailed)yield break;rig.ResetView();rig.SnapToTarget();selection.SelectNumber(3);
            foreach(var marker in route.Stone){yield return ChartClick((Vector3)marker.Cell+new Vector3(0,0.02f,0),marker,builder,rig);if(chartRouteFailed)yield break;}
            Check(route.StoneCount==3&&pile.Count("stone")==0&&!mission.TryActivate(),"Exact Stone stairs consume three units; remote chart activation still rejects");
            for(int jump=0;jump<2;jump++)
            {var forward=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);player.SetCommand(new Vector2(Vector3.Dot(Vector3.right,right),Vector3.Dot(Vector3.right,forward)),true,true);yield return WaitFrames(jump==0?13:34);player.SetCommand(Vector2.zero,false,false);yield return WaitFrames(45);Check(player.Grounded&&Mathf.Abs(player.transform.position.y-(jump+1))<0.08f,"Real held jump lands on chart route level "+(jump+1));}
            yield return ChartWalk(route.Chart.position,player,rig);if(chartRouteFailed)yield break;yield return WaitFrames(40);cutaway.RefreshNow();
            bool physical=true;foreach(var block in world.Blocks){physical&=block.Solid.enabled&&block.SolidShadow!=null;if(cutaway.IsObstruction(block))physical&=cutaway.TargetFor(block)<=0.04f;}
            Check(physical&&mission.SurveyedCount==3&&mission.TryActivate()&&mission.Stage==PaxExpeditionStage.Complete,"Unwarped traversal recovers the chart with canopy/collision/obstruction invariants");
            Check(mission.Objective.Contains("Ember Reach")&&pile.Count("timber")==0&&pile.Count("stone")==0&&save.SaveEdits(),"Chart reveals an original destination and completed route saves without resource rewards");
            int revision=world.Revision;Check(mission.TryActivate()&&world.Revision==revision&&pile.Count("stone")==0,"Repeated chart recovery is idempotent");
            yield return ChartWalk(route.UpperLanding.position+Vector3.forward,player,rig);if(chartRouteFailed)yield break;builder.SetPointer(new Vector2(-10000,-10000),false,false);yield return new WaitForSecondsRealtime(0.2f);Capture("Chart-02-Recovered-HUD.png");
            Check(save.LoadEdits(true)&&route.TimberCount==1&&route.StoneCount==0&&pile.Count("timber")==4&&pile.Count("stone")==3,"Previous partial-route snapshot restores exact mixed resources and matching repairs");yield return WaitFrames(3);Check(mission.Stage==PaxExpeditionStage.Repair,"Rollback revokes completed route state");
            bool stable=true;for(int i=0;i<3;i++)stable&=save.LoadEdits()&&route.TimberCount==5&&route.StoneCount==3&&pile.Count("timber")==0&&pile.Count("stone")==0;
            yield return WaitFrames(3);bool needsActivation=mission.Stage==PaxExpeditionStage.Return;
            yield return ChartWalk(route.Chart.position,player,rig);if(chartRouteFailed)yield break;yield return WaitFrames(3);
            Check(stable&&needsActivation&&mission.TryActivate(),"Repeated completed-snapshot loads do not multiply supplies and require explicit reactivation");
            yield return ChartWalk(route.UpperLanding.position+Vector3.forward,player,rig);if(chartRouteFailed)yield break;yield return new WaitForSecondsRealtime(0.2f);Capture("Chart-03-Retry-HUD.png");
            yield return ChartWalk(route.UpperLanding.position+Vector3.forward,player,rig);if(chartRouteFailed)yield break;
            revision=world.Revision;string bytes=File.ReadAllText(save.SavePath);mission.RestartSurvey();
            Check(mission.SurveyedCount==0&&mission.RepairCount==8&&world.Revision==revision&&pile.Count("timber")==0&&pile.Count("stone")==0&&bytes==File.ReadAllText(save.SavePath),"Replay keeps route, finite stocks and snapshot bytes; only discoveries restart");yield return new WaitForSecondsRealtime(0.2f);Capture("Chart-04-Replay-HUD.png");
        }

        private IEnumerator RunResources(PaxStockpile pile, PaxPlayer player, PaxWorld world, PaxBuilder builder, PaxCamera rig, PaxCutaway cutaway)
        {
            var selection=FindFirstObjectByType<PaxMaterialSelection>(); var mission=FindFirstObjectByType<PaxExpedition>(); var save=FindFirstObjectByType<PaxPlaytestSave>();
            save.UseValidationDirectory(Path.Combine(output,"ResourceSave-"+Guid.NewGuid().ToString("N")));
            Check(pile.Ready && pile.Palette.Entries.Count==5 && pile.Count("stone")==0,"Finite mode starts with deliberate five-material mapping and zero stocks");
            selection.SelectNumber(3); builder.SetPointer(rig.View.WorldToScreenPoint(new Vector3(1,0.02f,-5)),false,false);
            int revision=world.Revision;
            Check(!builder.Place(new Vector3Int(1,0,-5)) && pile.Count("stone")==0 && world.Revision==revision,"Empty stock refuses placement without changing tiles or units");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Resources-00-Empty-HUD.png");
            int mined=0; var cells=FindFirstObjectByType<PaxResourceCache>().Cells;
            foreach(var cell in cells) { bool success=builder.Mine(cell); if(success) mined++; Check(success && pile.Count("stone")==mined,"Authored Stone mining awards exactly once at "+cell); }
            Check(!builder.Mine(cells[0]) && pile.Count("stone")==6,"Repeated mining cannot award the removed node again");
            selection.SelectNumber(5); Check(!builder.Place(new Vector3Int(1,0,-5)) && pile.Count("stone")==6 && pile.Count("roof")==0,"Stocks are material-specific; wrong selected supply does not spend Stone"); selection.SelectNumber(3);
            Check(!builder.Mine(new Vector3Int(0,-3,-3)) && !builder.Place(new Vector3Int(0,0,-3)) && pile.Count("stone")==6,"Protected mining and player-overlap placement leave stocks unchanged");
            Check(save.SaveEdits() && PaxResourceSnapshot.Decode(PaxAtomicFile.Read(save.SavePath)).Counts["stone"]==6,"Gathered snapshot atomically saves stockpile and removed resource tiles");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Resources-01-Gathered-HUD.png");
            foreach(var site in mission.Sites)
            {
                player.Warp(site.transform.position); rig.SnapToTarget(); yield return WaitFrames(45); cutaway.RefreshNow();
                bool solid=true; foreach(var block in world.Blocks) { solid &= block.Solid.enabled && block.SolidShadow!=null; if(cutaway.IsObstruction(block)) solid &= cutaway.TargetFor(block)<=0.04f; }
                Check(site.Surveyed && solid,site.Title+" visit and cutaway/collision remain valid in finite mode");
            }
            player.Warp(new Vector3(1,0.03f,-3)); rig.SnapToTarget(); yield return WaitFrames(10);
            var ordered=new List<PaxRepairMarker>(mission.Repairs); ordered.Sort((a,b)=>{int c=a.Cell.z.CompareTo(b.Cell.z);return c!=0?c:a.Cell.x.CompareTo(b.Cell.x);});
            int placed=0; foreach(var repair in ordered) { bool success=builder.Place(repair.Cell); if(success) placed++; Check(success && pile.Count("stone")==6-placed,"Required Stone repair spends one unit only on success "+repair.Cell); }
            yield return WaitFrames(2);
            Check(mission.RepairCount==6 && pile.Count("stone")==0 && save.SaveEdits(),"Built snapshot records six solid repairs and spent stock together");
            var forward=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized; var right=Vector3.Cross(Vector3.up,forward);
            player.SetCommand(new Vector2(Vector3.Dot(Vector3.back,right),Vector3.Dot(Vector3.back,forward)),true,true); yield return WaitFrames(30); player.SetCommand(Vector2.zero,false,false); yield return WaitFrames(35);
            Check(player.Grounded && Mathf.Abs(player.transform.position.y-1)<0.06f && mission.TryActivate() && mission.Stage==PaxExpeditionStage.Complete,"Gather-spend-survey loop ends with real platform jump and beacon activation");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Resources-02-Complete-HUD.png");
            selection.SelectNumber(5); Check(builder.Mine(new Vector3Int(1,0,-5)) && pile.Count("stone")==1 && pile.Count("roof")==0,"Mining a placed Stone repair refunds its actual ID, not the selected Roof");
            yield return WaitFrames(2); Check(mission.Stage==PaxExpeditionStage.Repair,"Removing a finite repair revokes beacon completion");
            player.Warp(new Vector3(1,0.03f,-3)); rig.SnapToTarget(); yield return WaitFrames(10);
            bool repeated=true; for(int i=0;i<3;i++) repeated &= save.LoadEdits() && pile.Count("stone")==0 && world.Contains(new Vector3Int(1,0,-5));
            Check(repeated,"Repeated full loads restore absolute zero stock and complete tiles without multiplication");
            Check(save.LoadEdits(true) && pile.Count("stone")==6 && !world.Contains(new Vector3Int(1,0,-5)),"Previous full snapshot restores gathered stocks with unbuilt platform");
            Check(save.LoadEdits() && pile.Count("stone")==0 && world.Contains(new Vector3Int(1,0,-5)),"Current full snapshot restores spent stocks with built platform");
            var invalid=pile.Capture(); invalid.Counts["stone"]=1; revision=world.Revision; string backup=File.ReadAllText(save.BackupPath);
            File.WriteAllText(save.SavePath,invalid.Encode()); Check(!save.LoadEdits() && pile.Count("stone")==0 && world.Revision==revision,"A forged extra unit rejects both states before mutation");
            Check(save.SaveEdits() && backup==File.ReadAllText(save.BackupPath) && Directory.GetFiles(Path.GetDirectoryName(save.SavePath),"*.invalid-*").Length==1,"Incompatible resource primary is archived without replacing valid combined backup");
            File.WriteAllText(save.SavePath,PaxSaveFile.Encode(world.CaptureEdits())); Check(!save.LoadEdits() && pile.Count("stone")==0 && world.Revision==revision,"Tile-only import is explicitly refused in finite mode"); Check(save.SaveEdits(),"Valid combined snapshot can repair the rejected tile-only primary");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Resources-03-Restored-HUD.png");
            revision=world.Revision; string bytes=File.ReadAllText(save.SavePath); mission.RestartSurvey();
            Check(mission.SurveyedCount==0 && mission.RepairCount==6 && pile.Count("stone")==0 && revision==world.Revision && bytes==File.ReadAllText(save.SavePath),"Survey replay keeps resources, construction and snapshot bytes unchanged");
            yield return new WaitForSecondsRealtime(0.2f); Capture("Resources-04-Replay-HUD.png");
        }

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
            CloseCapture();
            Application.Quit(failures==0?0:1);
        }
        private void CloseCapture()
        {
            if(captureCamera!=null) captureCamera.targetTexture=null;
            if(captureDocument!=null && originalPanel!=null) captureDocument.panelSettings=originalPanel;
            if(capturePanel!=null) Destroy(capturePanel);
            if(captureTarget!=null) { captureTarget.Release(); Destroy(captureTarget); }
            captureCamera=null; captureDocument=null; captureTarget=null; capturePanel=null;
        }
    }
}
