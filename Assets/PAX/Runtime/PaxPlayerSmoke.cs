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
