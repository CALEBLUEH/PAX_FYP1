using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    // Opt-in proof using normal motor, pointer actions and manual snapshot input; owned files only.
    [DefaultExecutionOrder(200)]
    public sealed class PaxFrontierSmoke : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>(); private int failures;
        private string output, directory, phase;
        private PaxGeneratedFrontier frontier; private PaxWorld world; private PaxPlayer player; private PaxCamera rig; private PaxBuilder builder; private PaxStockpile stocks; private PaxPlaytestSave save; private PaxPause pause; private PaxInput input; private PaxExpedition mission;
        private Camera view; private UIDocument document; private PanelSettings original, panel; private RenderTexture target;
        private static string Arg(string key) { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args,key); return i >= 0 && i + 1 < args.Length ? args[i+1] : null; }
        private void Check(bool ok, string message) { checks.Add((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
        private IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../SmokeEvidence")); directory = Path.GetFullPath(Arg("-paxValidationDir") ?? output); phase = Arg("-paxFrontierPhase");
            if (!directory.StartsWith(output.TrimEnd('\\','/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || phase != "seed" && phase != "reload")
            { Debug.LogError("Frontier probe requires seed/reload and an owned SmokeEvidence child directory."); Application.Quit(2); yield break; }
            Directory.CreateDirectory(directory); Directory.CreateDirectory(output); Time.captureDeltaTime = 1f/60;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                bool more = false; object next = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; } catch (Exception caught) { error = caught; }
                if (error != null) { Check(false,"Fixture aborted: " + error.Message); break; }
                if (!more) { stack.Pop(); continue; } if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            CloseCapture(); File.WriteAllLines(Path.Combine(output,"Frontier-" + phase + "-Checks.txt"),checks);
            Debug.Log("PAX_FRONTIER_SMOKE " + phase + ": " + (checks.Count-failures) + "/" + checks.Count + " passed; owned directory " + directory); Time.timeScale = 1; Time.captureDeltaTime = 0; Application.Quit(failures == 0 ? 0 : 1);
        }
        private IEnumerator Run()
        {
            frontier = FindFirstObjectByType<PaxGeneratedFrontier>(); if (frontier == null) throw new InvalidOperationException("Expected saved generated frontier scene.");
            world=FindFirstObjectByType<PaxWorld>(); player=FindFirstObjectByType<PaxPlayer>(); rig=FindFirstObjectByType<PaxCamera>(); builder=FindFirstObjectByType<PaxBuilder>(); stocks=FindFirstObjectByType<PaxStockpile>(); save=FindFirstObjectByType<PaxPlaytestSave>(); pause=FindFirstObjectByType<PaxPause>(); input=FindFirstObjectByType<PaxInput>(); mission=frontier.Mission;
            pause.UseValidationInput(); input.enabled=false; FindFirstObjectByType<PaxExpeditionInput>().enabled=false; save.UseValidationDirectory(directory);
            yield return Release(); yield return Frames(20); SetupCapture(); yield return new WaitForSecondsRealtime(2); yield return Frames(12);
            Check(stocks.Ready && player.Grounded && world.Blocks.Count == frontier.AuthoredTileCount && stocks.Count("timber") == 0 && stocks.Count("stone") == 0 && world.CaptureEdits().Placed.Count == 0, "Saved generated scene starts grounded, finite and fresh; loading never regenerates or auto-restores");
            Check(mission.Repairs.Count(r=>r.RequiredMaterialId=="timber") == frontier.Definition.crossingLength && mission.Repairs.Count(r=>r.RequiredMaterialId=="stone") == frontier.Definition.lookoutHeight, "Authored variable repair budget matches the saved definition");
            Capture("Frontier-" + phase + "-00-Camp.png");
            pause.ProcessInput(true,true); yield return Frames(4); var position=player.transform.position; int revision=world.Revision;
            input.ApplyFrame(new PaxInputFrame { movement=Vector2.one, jumpPressed=true, jumpHeld=true, save=true, mine=true }); yield return Frames(5);
            Check(pause.IsPaused && Time.timeScale==0 && player.transform.position==position && world.Revision==revision && (phase!="seed" || !File.Exists(save.SavePath)), "Pause blocks movement, pointer actions and saving in the generated scene"); Capture("Frontier-" + phase + "-01-Pause.png");
            yield return Release(); pause.Resume(); yield return Release(); Check(!pause.BlocksActions && Time.timeScale==1,"Explicit Resume and neutral release restore controls");
            input.ApplyFrame(new PaxInputFrame { toggleCursor=true, toggleGhostTargets=true });
            var crossing=mission.Repairs.Where(r=>r.RequiredMaterialId=="timber").OrderBy(r=>r.Cell.z).ToArray(); var steps=mission.Repairs.Where(r=>r.RequiredMaterialId=="stone").OrderBy(r=>r.Cell.y).ToArray();
            if (phase=="reload")
            {
                string primary=File.ReadAllText(save.SavePath), backup=File.ReadAllText(save.BackupPath); input.ApplyFrame(new PaxInputFrame { load=true }); yield return Frames(5);
                Check(stocks.Capture().Encode()==PaxResourceSnapshot.Decode(File.ReadAllText(Path.Combine(directory,"ExpectedPrimary.xml"))).Encode(),"New Windows process: explicit F9 restores exact partial world, every stock and harvest history");
                Check(mission.SurveyedCount==0 && Vector3.Distance(player.transform.position,frontier.SpawnMarker.position)<0.1f,"Manual load keeps authored camp and starts with fresh session field notes");
                Check(primary==File.ReadAllText(save.SavePath) && backup==File.ReadAllText(save.BackupPath),"F9 reads current and backup without overwriting either file");
                input.ApplyFrame(new PaxInputFrame { previous=true }); yield return Frames(4); Check(stocks.Count("timber")==crossing.Length && mission.RepairCount==0,"Explicit F8 restores the earned gathered backup with matching stocks");
                input.ApplyFrame(new PaxInputFrame { load=true }); yield return Frames(4); Check(stocks.Count("timber")==crossing.Length-1 && crossing[0].IsSolid,"Explicit F9 restores partial repair and matching spent unit together"); Capture("Frontier-reload-02-Partial.png");
            }
            foreach (string kind in new[] {"House","Cave"})
            {
                var structure=frontier.Structures.Single(s=>s.kind==kind); yield return Walk(new Vector3(0,0.03f,structure.door.z-2)); yield return Walk(new Vector3(structure.centre.x,0.03f,structure.door.z-2));
                if (phase=="seed")
                    foreach (var cell in frontier.Cache.Cells.Where(c=>kind=="House" ? c.x<0 : c.x>0)) { yield return Walk(new Vector3(cell.x,0.03f,structure.door.z-2)); yield return Mine(cell); }
                else if (kind=="House")
                {
                    var cacheCell=frontier.Cache.Cells.First(c=>c.x<0); int before=stocks.Count("timber");
                    yield return Walk(new Vector3(cacheCell.x,0.03f,structure.door.z-2));
                    for(int i=0;i<3;i++) { yield return Place(cacheCell,"timber"); yield return Mine(cacheCell); }
                    Check(stocks.Count("timber")==before && stocks.Capture().Harvested.Count==crossing.Length+steps.Length,"Restored once-only harvest history prevents rebuild/mining farming");
                }
                yield return Walk(new Vector3(structure.centre.x,0.03f,structure.door.z-2));
                yield return Walk((Vector3)structure.door+Vector3.up*0.03f); yield return Walk((Vector3)structure.centre+Vector3.up*0.03f); yield return Frames(35);
                Check(mission.Sites.Single(s=>s.Title.StartsWith(kind,StringComparison.Ordinal)).Surveyed,"Normal walking enters generated " + kind + " doorway and earns its survey note");
                Check(world.Blocks.All(b=>b.Solid.enabled),"Generated " + kind + " cutaway keeps every tile collider enabled"); Capture("Frontier-"+phase+"-"+kind+".png");
                yield return Walk((Vector3)structure.door+Vector3.up*0.03f); yield return Walk(new Vector3(structure.centre.x,0.03f,structure.door.z-2)); yield return Walk(new Vector3(0,0.03f,structure.door.z-2));
            }
            if(phase=="seed")
            {
                Check(stocks.Count("timber")==crossing.Length && stocks.Count("stone")==steps.Length && stocks.Capture().Harvested.Count==crossing.Length+steps.Length,"Normal pointer mining earns the exact generated repair budget"); input.ApplyFrame(new PaxInputFrame {save=true}); Check(File.Exists(save.SavePath),"Explicit F5 saves earned gathered world/resources"); Capture("Frontier-seed-02-Gathered.png");
                yield return Walk(frontier.NearBank.position); yield return Place(crossing[0].Cell,"timber"); Check(crossing[0].IsSolid,"First crossing blueprint accepts the earned Timber"); input.ApplyFrame(new PaxInputFrame {save=true});
                File.Copy(save.SavePath,Path.Combine(directory,"ExpectedPrimary.xml"),true); File.Copy(save.BackupPath,Path.Combine(directory,"ExpectedBackup.xml"),true);
                Check(stocks.Count("timber")==crossing.Length-1 && stocks.Count("stone")==steps.Length && File.ReadAllText(save.SavePath)!=File.ReadAllText(save.BackupPath),"Second F5 saves a distinct partial edit with conserved world/stock/history"); Capture("Frontier-seed-03-Saved.png"); yield break;
            }
            yield return Walk(frontier.NearBank.position);
            foreach(var repair in crossing) { if(!repair.IsSolid) yield return Place(repair.Cell,"timber"); yield return Walk((Vector3)repair.Cell+Vector3.up*1.03f); Check(player.Grounded,"Normal walking crosses repaired cell " + repair.Cell); }
            yield return Walk(frontier.FarBank.position);
            foreach(var repair in steps) yield return Place(repair.Cell,"stone");
            foreach(var repair in steps) { yield return Jump((Vector3)repair.Cell+Vector3.up*1.03f); Check(player.Grounded && Mathf.Abs(player.transform.position.y-repair.Cell.y-1)<0.1f,"Normal held jump lands on generated Stone step " + repair.Cell); }
            yield return Walk(mission.BeaconReturn.position); yield return Frames(35); Check(mission.SurveyedCount==3 && mission.RepairCount==mission.Repairs.Length && mission.AtBeacon,"Physical generated route reaches all survey marks and matching repairs without Warp");
            Check(mission.TryActivate() && mission.Stage==PaxExpeditionStage.Complete && stocks.Count("timber")==0 && stocks.Count("stone")==0,"Existing F activation completes the shelter relay with the exact earned budget");
            builder.SetPointer(new Vector2(-10000,-10000),false,false); rig.ResetView(); rig.Zoom(-3); yield return Frames(15); Capture("Frontier-reload-03-Connected.png");
            Check(File.ReadAllText(save.SavePath)==File.ReadAllText(Path.Combine(directory,"ExpectedPrimary.xml")) && File.ReadAllText(save.BackupPath)==File.ReadAllText(Path.Combine(directory,"ExpectedBackup.xml")),"Completing the route never autosaves or changes current/backup bytes");
            input.ApplyFrame(new PaxInputFrame {load=true}); yield return Frames(5); Check(mission.RepairCount==1 && mission.Stage!=PaxExpeditionStage.Complete && stocks.Count("timber")==crossing.Length-1 && stocks.Count("stone")==steps.Length,"F9 rechecks mission repairs after restoring partial world and stocks");
            // Leave the visit radius by normal walking before F6, then wait for the unscaled HUD
            // clock. Fast diagnostic frames alone can capture the preceding label refresh.
            yield return Walk(mission.BeaconReturn.position+new Vector3(1,0,1));
            mission.RestartSurvey(); yield return new WaitForSecondsRealtime(0.2f); yield return Frames(3);
            Check(mission.SurveyedCount==0 && mission.RepairCount==1 && stocks.Count("timber")==crossing.Length-1,"F6 resets only the session journal, preserving finite state"); Capture("Frontier-reload-04-Replay.png");
        }
        private IEnumerator Release() { for(int i=0;i<4;i++) { pause.ProcessInput(false,false); yield return null; } }
        private static IEnumerator Frames(int count) { for(int i=0;i<count;i++) yield return null; }
        private IEnumerator Walk(Vector3 destination)
        {
            for(int i=0;i<600;i++) { var delta=destination-player.transform.position; delta.y=0; if(delta.magnitude<0.09f) { player.ClearCommand(); yield break; } var f=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized; var r=Vector3.Cross(Vector3.up,f); input.ApplyFrame(new PaxInputFrame {movement=new Vector2(Vector3.Dot(delta.normalized,r),Vector3.Dot(delta.normalized,f))}); yield return null; }
            player.ClearCommand(); throw new InvalidOperationException("Physical walk blocked toward " + destination + "; actor=" + player.transform.position);
        }
        private IEnumerator Jump(Vector3 destination)
        {
            for(int i=0;i<150;i++) { var delta=destination-player.transform.position; delta.y=0; if(i>10 && player.Grounded && Mathf.Abs(player.transform.position.y-destination.y)<0.1f && delta.magnitude<0.12f) { player.ClearCommand(); yield break; } var f=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized; var r=Vector3.Cross(Vector3.up,f); var direction=delta.magnitude<0.07f?Vector3.zero:delta.normalized; input.ApplyFrame(new PaxInputFrame {movement=new Vector2(Vector3.Dot(direction,r),Vector3.Dot(direction,f)),jumpPressed=i==0,jumpHeld=true}); yield return null; }
            player.ClearCommand(); throw new InvalidOperationException("Physical jump failed toward " + destination + "; actor=" + player.transform.position);
        }
        private IEnumerator Mine(Vector3Int cell)
        {
            bool found=false; Vector3 point=Vector3.zero;
            for(int angle=0;angle<360;angle+=45) { rig.ResetView(); rig.Orbit(angle,0); rig.SnapToTarget(); point=rig.View.WorldToScreenPoint(PaxGrid.Centre(cell)+Vector3.up*0.501f); builder.Select(point); if(builder.CandidateCell==cell && builder.MiningBlock==PaxActionBlock.None) {found=true;break;} }
            if(!found) throw new InvalidOperationException("No normal mining pointer for " + cell + ": " + builder.MiningStatus);
            builder.SetPointer(point,true,false); yield return Frames(3); Check(!world.Contains(cell),"Normal pointer mining at " + cell); if(world.Contains(cell)) throw new InvalidOperationException(builder.LastAction);
        }
        private IEnumerator Place(Vector3Int cell,string id)
        {
            var selection=FindFirstObjectByType<PaxMaterialSelection>(); int number=selection.Palette.Entries.ToList().FindIndex(e=>e.id==id)+1;
            if(!selection.SelectNumber(number)) throw new InvalidOperationException("Material selection unavailable."); bool found=false; Vector3 point=Vector3.zero;
            foreach(var direction in new[] {Vector3Int.forward,Vector3Int.up,Vector3Int.right,Vector3Int.left,Vector3Int.back})
            {
                if(!world.Contains(cell-direction)) continue;
                for(int angle=0;angle<360;angle+=45) { rig.ResetView(); rig.Orbit(angle,0); rig.SnapToTarget(); point=rig.View.WorldToScreenPoint(PaxGrid.Centre(cell-direction)+(Vector3)direction*0.501f); builder.Select(point); if(builder.Placement==cell && builder.CanPlace) {found=true;break;} } if(found) break;
            }
            if(!found) throw new InvalidOperationException("No normal placement pointer for " + cell + ": " + builder.PlacementStatus);
            builder.SetPointer(point,false,true); yield return Frames(3); Check(world.Contains(cell),"Normal pointer construction with " + id + " at " + cell); if(!world.Contains(cell)) throw new InvalidOperationException(builder.LastAction);
        }
        private void SetupCapture() { view=Camera.main; document=FindFirstObjectByType<UIDocument>(); original=document.panelSettings; target=new RenderTexture(1280,720,24);target.Create();view.targetTexture=target;panel=Instantiate(original);panel.targetTexture=target;panel.clearColor=false;document.panelSettings=panel; }
        private void LateUpdate() { if(view!=null&&target!=null)view.Render(); }
        private void Capture(string name) { var old=RenderTexture.active;var bitmap=new Texture2D(1280,720,TextureFormat.RGB24,false);try{RenderTexture.active=target;bitmap.ReadPixels(new Rect(0,0,1280,720),0,0);bitmap.Apply();File.WriteAllBytes(Path.Combine(output,name),bitmap.EncodeToPNG());}finally{RenderTexture.active=old;Destroy(bitmap);} }
        private void CloseCapture() { if(view!=null)view.targetTexture=null;if(document!=null&&original!=null)document.panelSettings=original;if(panel!=null)Destroy(panel);if(target!=null){target.Release();Destroy(target);} }
    }
}
