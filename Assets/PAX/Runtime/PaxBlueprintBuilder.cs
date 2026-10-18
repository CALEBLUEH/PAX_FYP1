using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    [DefaultExecutionOrder(-50)]
    public sealed class PaxBlueprintBuilder : MonoBehaviour
    {
        [SerializeField] private PaxBuilder builder;
        [SerializeField] private PaxPause pause;
        [SerializeField] private PaxPlaytestSave storage;
        [SerializeField] private UIDocument document;
        [SerializeField] private PaxConstructionBlueprint[] definitions;
        [SerializeField] private Renderer ghostPrefab;
        [SerializeField] private Transform ghostRoot;
        [SerializeField] private Material validMaterial, invalidMaterial;
        [SerializeField] private KeyCode bridgeKey = KeyCode.B, stepsKey = KeyCode.N, rotateKey = KeyCode.T, cancelKey = KeyCode.Backspace;
        private readonly List<Renderer> ghosts = new List<Renderer>();
        private PaxConstructionBlueprint selected;
        private int turns, transitionFrame = -1;
        private bool awaitingRelease = true, hardware = true, bridgeWasHeld, stepsWereHeld, rotateWasHeld, cancelWasHeld;
        public bool Active => selected != null;
        public IReadOnlyList<PaxConstructionBlueprint> Definitions => definitions;
        public bool BlocksPointerActions => awaitingRelease || Time.frameCount <= transitionFrame;
        public bool PreviewValid { get; private set; }
        public int VisibleGhostCount { get { int n=0;foreach(var g in ghosts)if(g!=null&&g.gameObject.activeSelf)n++;return n; } }
        public int QuarterTurns => turns;
        public string Status { get; private set; } = "Single-cell: 1–5. B Timber pair / N Stone steps / T rotate / Backspace cancel.";
        public void Configure(PaxBuilder placement, PaxPause owner, PaxPlaytestSave save, UIDocument ui, PaxConstructionBlueprint[] assets, Renderer prefab, Transform root, Material valid, Material invalid)
        { builder=placement;pause=owner;storage=save;document=ui;definitions=assets;ghostPrefab=prefab;ghostRoot=root;validMaterial=valid;invalidMaterial=invalid; }
        private void OnEnable() { if(!Application.isPlaying||builder==null)return;builder.UseBlueprints(this);storage.LoadRequested+=Cancel;OnActionsCleared(); }
        private void OnDisable() { if(!Application.isPlaying||builder==null)return;storage.LoadRequested-=Cancel;builder.ClearPendingActions();builder.UseBlueprints(null);Hide();foreach(var ghost in ghosts)if(ghost!=null)Destroy(ghost.gameObject);ghosts.Clear(); }
        private void Update()
        {
            if(hardware)ProcessInput(Input.GetKey(bridgeKey),Input.GetKey(stepsKey),Input.GetKey(rotateKey),Input.GetKey(cancelKey),Input.anyKey||Input.mouseScrollDelta.sqrMagnitude>0);
            if(pause.BlocksActions||!builder.isActiveAndEnabled){builder.ClearPendingActions();return;}
            if(!Active)Present();
        }
        public void UseValidationInput()
        { if(!Application.isEditor&&Array.IndexOf(Environment.GetCommandLineArgs(),"-paxSmoke")<0)throw new InvalidOperationException("Explicit diagnostic input only.");hardware=false;OnActionsCleared(); }
        public void ProcessInput(bool bridgeHeld,bool stepsHeld,bool rotateHeld,bool cancelHeld,bool anyHeld)
        {
            bool b=bridgeHeld&&!bridgeWasHeld,n=stepsHeld&&!stepsWereHeld,t=rotateHeld&&!rotateWasHeld,c=cancelHeld&&!cancelWasHeld;
            bridgeWasHeld=bridgeHeld;stepsWereHeld=stepsHeld;rotateWasHeld=rotateHeld;cancelWasHeld=cancelHeld;
            if(!isActiveAndEnabled||pause.BlocksActions){OnActionsCleared();return;}
            if(awaitingRelease&&!anyHeld&&!bridgeHeld&&!stepsHeld&&!rotateHeld&&!cancelHeld){awaitingRelease=false;transitionFrame=Time.frameCount;}
            if(BlocksPointerActions)return;
            if(c)Cancel();else if(b)SelectBlueprint(0);else if(n)SelectBlueprint(1);else if(t&&Active)Set(selected,(turns+1)%4);
        }
        public bool SelectBlueprint(int index)
        {
            if(!isActiveAndEnabled||pause.BlocksActions||definitions==null||index<0||index>=definitions.Length)return false;
            var definition=definitions[index];
            string reason=null;
            if(definition==null||!definition.TryPlan(Vector3Int.zero,0,out _,out reason)){Cancel();Status="Invalid blueprint; single-cell kept. "+(definition==null?"Missing asset.":reason);Present();return false;}
            Set(definition,0);return true;
        }
        private void Set(PaxConstructionBlueprint definition,int rotation){builder.ClearPendingActions();selected=definition;turns=rotation;Status="Release held controls to preview "+definition.Title+".";Present();}
        public void Cancel()=>builder.ClearPendingActions();
        public void OnActionsCleared()
        { selected=null;turns=0;awaitingRelease=true;transitionFrame=Time.frameCount;bridgeWasHeld=stepsWereHeld=rotateWasHeld=cancelWasHeld=true;PreviewValid=false;Hide();Status="Single-cell: 1–5. B Timber pair / N Stone steps / T rotate / Backspace cancel.";Present(); }
        private void Hide(){foreach(var g in ghosts)if(g!=null)g.gameObject.SetActive(false);PreviewValid=false;}
        public void RefreshPreview()
        {
            if(!Active){Hide();return;}PreviewValid=false;
            if(pause.BlocksActions||BlocksPointerActions){Hide();Status="Release held controls; blueprint has not placed or spent anything.";Present();return;}
            if(!builder.CandidateCell.HasValue){Hide();Status="Aim at a world tile to preview the footprint.";Present();return;}
            if(!selected.TryPlan(builder.Placement,turns,out var plan,out string malformed)){Hide();Status=malformed;Present();return;}
            bool valid=builder.EvaluateBlueprint(selected,builder.Placement,turns,out var costs,out string reason);
            valid &= builder.SelectedCell.HasValue;
            if(!builder.SelectedCell.HasValue)reason="Aim at a reachable world tile; no valid pointer target.";
            if(ghostPrefab==null||ghostRoot==null||ghostPrefab.GetComponentsInChildren<Collider>(true).Length!=0){Hide();Status="Ghost prefab configuration unavailable; nothing placed.";Present();return;}
            while(ghosts.Count<plan.Length)ghosts.Add(Instantiate(ghostPrefab,ghostRoot));
            for(int i=0;i<plan.Length;i++){ghosts[i].transform.position=PaxGrid.Centre(plan[i].Cell);ghosts[i].sharedMaterial=valid?validMaterial:invalidMaterial;ghosts[i].gameObject.SetActive(true);}
            for(int i=plan.Length;i<ghosts.Count;i++)ghosts[i].gameObject.SetActive(false);
            PreviewValid=valid;var text=new StringBuilder(selected.Title).Append(" / ").Append(turns*90).Append("° / ").Append(plan.Length).Append(" cells / Cost: ");var keys=new List<string>(costs.Keys);keys.Sort(StringComparer.Ordinal);
            foreach(var id in keys)text.Append(costs[id]).Append(' ').Append(id).Append("  ");
            Status=text.Append("\n").Append(valid?"READY: RMB places the entire footprint once.":"BLOCKED: "+reason).Append("\nT rotate · Backspace or 1–5 single-cell. Mining refunds actual material.").ToString();Present();
        }
        public bool TryPlaceFromPointer()
        {
            if(!Active||BlocksPointerActions||pause.BlocksActions||!builder.SelectedCell.HasValue)return false;
            RefreshPreview();if(!PreviewValid)return false;
            return builder.PlaceBlueprint(selected,builder.Placement,turns);
        }
        private void Present(){if(document==null)return;var label=document.rootVisualElement.Q<Label>("construction-blueprint");if(label!=null)label.text=Status;}
    }
}
