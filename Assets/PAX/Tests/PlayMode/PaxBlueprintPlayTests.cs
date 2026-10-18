using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    // Geometry edge fixtures use explicit test-only actor warps; Windows proof earns the full route normally.
    public sealed class PaxBlueprintPlayTests
    {
        private PaxBuilder builder;private PaxBlueprintBuilder blueprints;private PaxPause pause;private PaxPlayer player;private PaxWorld world;private PaxStockpile stocks;private PaxPlaytestSave save;private PaxCamera rig;private string directory;
        private readonly List<PaxConstructionBlueprint> temporary=new List<PaxConstructionBlueprint>();
        private Vector3Int Anchor=>Object.FindFirstObjectByType<PaxExpedition>().Repairs.First(r=>r.RequiredMaterialId=="timber").Cell;
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;Time.captureDeltaTime=1f/60;directory=Path.Combine(Path.GetTempPath(),"PaxBlueprintTests",Guid.NewGuid().ToString("N"));yield return SceneManager.LoadSceneAsync("PaxGeneratedFrontier");
            builder=Object.FindFirstObjectByType<PaxBuilder>();blueprints=Object.FindFirstObjectByType<PaxBlueprintBuilder>();pause=Object.FindFirstObjectByType<PaxPause>();player=Object.FindFirstObjectByType<PaxPlayer>();world=Object.FindFirstObjectByType<PaxWorld>();stocks=Object.FindFirstObjectByType<PaxStockpile>();save=Object.FindFirstObjectByType<PaxPlaytestSave>();rig=Object.FindFirstObjectByType<PaxCamera>();
            Object.FindFirstObjectByType<PaxInput>().enabled=false;Object.FindFirstObjectByType<PaxExpeditionInput>().enabled=false;pause.UseValidationInput();blueprints.UseValidationInput();save.UseValidationDirectory(directory);yield return Release();yield return Frames(10);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var d in temporary)Object.Destroy(d);temporary.Clear();if(pause!=null)pause.enabled=false;Time.timeScale=1;Time.captureDeltaTime=0;if(Directory.Exists(directory)){Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(),"PaxBlueprintTests")),Path.GetDirectoryName(Path.GetFullPath(directory)));Directory.Delete(directory,true);}yield return null;
        }
        private IEnumerator Release(){for(int i=0;i<6;i++){pause.ProcessInput(false,false);blueprints.ProcessInput(false,false,false,false,false);yield return null;}}
        private IEnumerator Gather(int timber=3,bool stone=true)
        {
            var cache=Object.FindFirstObjectByType<PaxResourceCache>();int count=0;
            foreach(var cell in cache.Cells)
            {
                bool isTimber=world.TryGet(cell,out var tile)&&tile.SurfaceMaterial.name=="Timber";if(isTimber&&count++>=timber||!isTimber&&!stone)continue;
                player.Warp((Vector3)cell+new Vector3(0,0.03f,1));yield return Frames(3);Assert.IsTrue(builder.Mine(cell),cell.ToString());
            }
            player.Warp(Object.FindFirstObjectByType<PaxGeneratedFrontier>().NearBank.position);yield return Frames(3);
        }
        private PaxConstructionBlueprint Definition(Vector3Int second,string first="timber",string other="timber")
        {var d=ScriptableObject.CreateInstance<PaxConstructionBlueprint>();d.Configure("Test footprint",new[]{new PaxConstructionBlueprint.Cell{offset=Vector3Int.zero,materialId=first},new PaxConstructionBlueprint.Cell{offset=second,materialId=other}});temporary.Add(d);return d;}
        private void Kept(string before,int revision){Assert.AreEqual(before,stocks.Capture().Encode());Assert.AreEqual(revision,world.Revision);}
        private void Aim(Vector3Int anchor)
        {
            builder.ToggleCursor();builder.ToggleGhostTargets();bool found=false;
            for(int angle=0;angle<360;angle+=45){rig.ResetView();rig.Orbit(angle,0);rig.SnapToTarget();var point=rig.View.WorldToScreenPoint(PaxGrid.Centre(anchor-Vector3Int.forward)+Vector3.forward*0.501f);builder.Select(point);if(builder.SelectedCell.HasValue&&builder.Placement==anchor){builder.SetPointer(point,false,false);found=true;break;}}
            Assert.IsTrue(found,"Reachable pointer anchor");
        }
        [UnityTest] public IEnumerator AggregateShortagePlacesNothingEvenThoughOneUnitCouldBuildTheFirstCell()
        {yield return Gather(1,false);string before=stocks.Capture().Encode();int r=world.Revision;Assert.AreEqual(1,stocks.Count("timber"));Assert.IsFalse(builder.PlaceBlueprint(blueprints.Definitions[0],Anchor,0));Kept(before,r);Assert.That(builder.LastAction,Does.Contain("aggregate"));}
        [UnityTest] public IEnumerator BlockedSecondCellDoesNotPlaceOrChargeTheValidFirstCell()
        {
            yield return Gather();builder.TrySelectMaterial("timber");Assert.IsTrue(builder.Place(Anchor));player.Warp((Vector3)Anchor+Vector3.up*1.03f);yield return Frames(3);Assert.IsTrue(builder.Place(Anchor+Vector3Int.forward));player.Warp(Object.FindFirstObjectByType<PaxGeneratedFrontier>().NearBank.position);yield return Frames(3);Assert.IsTrue(builder.Mine(Anchor));string before=stocks.Capture().Encode();int r=world.Revision;
            Assert.IsFalse(builder.PlaceBlueprint(blueprints.Definitions[0],Anchor,0));Kept(before,r);Assert.IsFalse(world.Contains(Anchor));Assert.That(builder.LastAction,Does.Contain("occupied"));
        }
        [UnityTest] public IEnumerator SupportedPairCommitsOnceAndRefundsActualMaterialsWithoutFarming()
        {
            yield return Gather();int r=world.Revision;
            for(int i=0;i<3;i++){Assert.IsTrue(builder.PlaceBlueprint(blueprints.Definitions[0],Anchor,0));Assert.AreEqual(r+1,world.Revision);Assert.AreEqual(1,stocks.Count("timber"));foreach(var c in new[]{Anchor,Anchor+Vector3Int.forward}){Assert.IsTrue(world.TryGet(c,out var tile));Assert.AreEqual("Timber",tile.SurfaceMaterial.name);}builder.TrySelectMaterial("roof");Assert.IsTrue(builder.Mine(Anchor));Assert.IsTrue(builder.Mine(Anchor+Vector3Int.forward));Assert.AreEqual(3,stocks.Count("timber"));Assert.AreEqual(5,stocks.Capture().Harvested.Count);r=world.Revision;}
        }
        [UnityTest] public IEnumerator MixedMaterialsUseAggregateCostsAndInvalidMaterialOrDuplicateIsAtomic()
        {
            yield return Gather();var mixed=Definition(Vector3Int.forward,"timber","stone");Assert.IsTrue(builder.PlaceBlueprint(mixed,Anchor,0));Assert.AreEqual(2,stocks.Count("timber"));Assert.AreEqual(1,stocks.Count("stone"));builder.TrySelectMaterial("default");Assert.IsTrue(builder.Mine(Anchor));Assert.IsTrue(builder.Mine(Anchor+Vector3Int.forward));Assert.AreEqual(3,stocks.Count("timber"));Assert.AreEqual(2,stocks.Count("stone"));
            string before=stocks.Capture().Encode();int r=world.Revision;Assert.IsFalse(builder.PlaceBlueprint(Definition(Vector3Int.forward,"timber","unmapped"),Anchor,0));Kept(before,r);Assert.IsFalse(builder.PlaceBlueprint(Definition(Vector3Int.zero,"timber","stone"),Anchor,0));Kept(before,r);
        }
        [UnityTest] public IEnumerator FarSecondCellCannotBorrowTheFirstCellsReach()
        {yield return Gather();string before=stocks.Capture().Encode();int r=world.Revision;Assert.IsFalse(builder.PlaceBlueprint(Definition(new Vector3Int(0,0,4)),Anchor,0));Kept(before,r);Assert.That(builder.LastAction,Does.Contain("within 5"));}
        [UnityTest] public IEnumerator BodyAndBoundsRejectTheEntireFootprint()
        {
            yield return Gather();string before=stocks.Capture().Encode();int r=world.Revision;Assert.IsFalse(builder.PlaceBlueprint(Definition(Vector3Int.left),new Vector3Int(1,0,3),0));Kept(before,r);Assert.That(builder.LastAction,Does.Contain("Player occupies"));
            player.Warp(new Vector3(world.Maximum.x-1,3.03f,0));Physics.SyncTransforms();Assert.IsFalse(builder.PlaceBlueprint(Definition(Vector3Int.right),new Vector3Int(world.Maximum.x,4,0),0));Kept(before,r);Assert.That(builder.LastAction,Does.Contain("bounds"));
        }
        [UnityTest] public IEnumerator FloatingPairCannotSelfSupport()
        {yield return Gather();player.Warp(new Vector3(2,4.03f,3));Physics.SyncTransforms();string before=stocks.Capture().Encode();int r=world.Revision;Assert.IsFalse(builder.PlaceBlueprint(Definition(Vector3Int.forward),new Vector3Int(0,5,3),0));Kept(before,r);Assert.That(builder.LastAction,Does.Contain("existing support"));}
        [UnityTest] public IEnumerator PreviewRevalidatesAggregateBudgetAfterAnInterveningSingleCellSpend()
        {
            yield return Gather(2,false);builder.TrySelectMaterial("timber");yield return Release();Aim(Anchor);Assert.IsTrue(blueprints.SelectBlueprint(0));yield return Release();blueprints.RefreshPreview();Assert.IsTrue(blueprints.PreviewValid);Assert.AreEqual(2,blueprints.VisibleGhostCount);
            Assert.IsTrue(builder.Place(new Vector3Int(1,0,3)));string before=stocks.Capture().Encode();int r=world.Revision;Assert.IsFalse(blueprints.TryPlaceFromPointer());Kept(before,r);Assert.IsFalse(world.Contains(Anchor));
        }
        [UnityTest] public IEnumerator HeldSelectionFocusAndLoadClearPreviewWithoutClickThrough()
        {
            yield return Gather();Aim(Anchor);blueprints.ProcessInput(true,false,false,false,true);Assert.IsTrue(blueprints.Active);string before=stocks.Capture().Encode();int r=world.Revision;builder.SetPointer(rig.View.WorldToScreenPoint(PaxGrid.Centre(Anchor-Vector3Int.forward)+Vector3.forward*0.501f),false,true);yield return Frames(3);Kept(before,r);
            yield return Release();blueprints.RefreshPreview();Assert.IsTrue(blueprints.PreviewValid);pause.ProcessFocus(false);Assert.IsFalse(blueprints.Active);Assert.AreEqual(0,blueprints.VisibleGhostCount);Assert.IsFalse(builder.PlaceBlueprint(blueprints.Definitions[0],Anchor,0));Kept(before,r);
            pause.ProcessFocus(true);yield return Release();Assert.IsTrue(pause.IsPaused);pause.Resume();yield return Release();Assert.IsTrue(blueprints.SelectBlueprint(0));yield return Release();blueprints.RefreshPreview();Assert.AreEqual(2,blueprints.VisibleGhostCount);Assert.IsFalse(save.LoadEdits());Assert.IsFalse(blueprints.Active);Assert.AreEqual(0,blueprints.VisibleGhostCount);
            Assert.IsTrue(blueprints.SelectBlueprint(1));yield return Release();blueprints.enabled=false;Assert.AreEqual(0,blueprints.VisibleGhostCount);Kept(before,r);
        }
        [UnityTest] public IEnumerator TypedPairSnapshotRoundTripKeepsOriginalSignatureStockAndHarvestHistory()
        {
            yield return Gather();string signature=world.SaveSignature;Assert.AreEqual("0FF516D17A4776476390C87ABDB48370A93E1C1B66AE68333A4B27C4A1E8C11B",signature);Assert.IsTrue(builder.PlaceBlueprint(blueprints.Definitions[0],Anchor,0));Assert.IsTrue(save.SaveEdits());string expected=stocks.Capture().Encode();Assert.IsTrue(builder.Mine(Anchor));Assert.IsTrue(builder.Mine(Anchor+Vector3Int.forward));Assert.IsTrue(save.LoadEdits());Assert.AreEqual(expected,stocks.Capture().Encode());Assert.AreEqual(signature,world.SaveSignature);Assert.AreEqual(2,world.CaptureEdits().Placed.Count);Assert.IsTrue(stocks.Capture().Tiles.Materials.Values.All(id=>id=="timber"));
        }
        [UnityTest] public IEnumerator CardinalRotationAndLostPointerCannotPlaceOrLeaveStaleGhosts()
        {
            yield return Gather();Aim(Anchor);Assert.IsTrue(blueprints.SelectBlueprint(0));yield return Release();blueprints.RefreshPreview();Assert.IsTrue(blueprints.PreviewValid);string before=stocks.Capture().Encode();int r=world.Revision;
            for(int i=0;i<2;i++){blueprints.ProcessInput(false,false,true,false,true);Assert.AreEqual(0,blueprints.VisibleGhostCount);yield return Release();}
            blueprints.RefreshPreview();Assert.AreEqual(2,blueprints.QuarterTurns);Assert.IsFalse(blueprints.PreviewValid);Assert.AreEqual(2,blueprints.VisibleGhostCount);Assert.IsFalse(blueprints.TryPlaceFromPointer());Kept(before,r);
            builder.Select(new Vector2(-10000,-10000));blueprints.RefreshPreview();Assert.AreEqual(0,blueprints.VisibleGhostCount);Assert.IsFalse(blueprints.TryPlaceFromPointer());Kept(before,r);
        }
        [UnityTest] public IEnumerator APhysicalObstructionToTheSecondCellRejectsTheWholeFootprint()
        {
            yield return Gather();var first=new Vector3Int(1,0,3);var second=first+Vector3Int.forward;var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0.5f,0.75f,3.5f);blocker.transform.localScale=Vector3.one*0.3f;Physics.SyncTransforms();
            try {Assert.IsFalse(builder.HasLineOfSight(second));Assert.IsTrue(builder.HasLineOfSight(first));string before=stocks.Capture().Encode();int r=world.Revision;Assert.IsFalse(builder.PlaceBlueprint(Definition(Vector3Int.forward),first,0));Kept(before,r);}
            finally{Object.Destroy(blocker);}yield return null;
        }
    }
}
