using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxChartRouteTests
    {
        private PaxWorld world;private PaxBuilder builder;private PaxPlayer player;private PaxCamera rig;private PaxChartRoute route;private PaxStockpile pile;private PaxExpedition mission;private PaxPlaytestSave save;private string directory;
        [UnitySetUp]public IEnumerator Setup()
        {Time.captureDeltaTime=1f/60;yield return SceneManager.LoadSceneAsync("PaxLamplighterChart");world=Object.FindFirstObjectByType<PaxWorld>();builder=Object.FindFirstObjectByType<PaxBuilder>();player=Object.FindFirstObjectByType<PaxPlayer>();rig=Object.FindFirstObjectByType<PaxCamera>();route=Object.FindFirstObjectByType<PaxChartRoute>();pile=Object.FindFirstObjectByType<PaxStockpile>();mission=Object.FindFirstObjectByType<PaxExpedition>();save=Object.FindFirstObjectByType<PaxPlaytestSave>();Object.FindFirstObjectByType<PaxInput>().enabled=false;Object.FindFirstObjectByType<PaxExpeditionInput>().enabled=false;directory=Path.Combine(Path.GetTempPath(),"PaxChartTests",Guid.NewGuid().ToString("N"));save.UseValidationDirectory(directory);yield return Frames(15);}
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureDeltaTime=0;Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(),"PaxChartTests")),Path.GetDirectoryName(Path.GetFullPath(directory)));if(Directory.Exists(directory))Directory.Delete(directory,true);yield return null;}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        private void Gather(){foreach(var cell in Object.FindFirstObjectByType<PaxResourceCache>().Cells)Assert.IsTrue(builder.Mine(cell),cell.ToString());Assert.AreEqual(5,pile.Count("timber"));Assert.AreEqual(3,pile.Count("stone"));}
        private IEnumerator Walk(Vector3 target)
        {
            for(int i=0;i<180;i++){var delta=target-player.transform.position;delta.y=0;if(delta.magnitude<0.09f){player.SetCommand(Vector2.zero,false,false);yield break;}var f=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;var r=Vector3.Cross(Vector3.up,f);player.SetCommand(new Vector2(Vector3.Dot(delta.normalized,r),Vector3.Dot(delta.normalized,f)),false,false);yield return null;}
            player.SetCommand(Vector2.zero,false,false);Assert.Fail("Route walk blocked toward "+target+" at "+player.transform.position);
        }
        private IEnumerator Click(Vector3 surface,PaxRepairMarker expected)
        {rig.SnapToTarget();builder.SetPointer(rig.View.WorldToScreenPoint(surface),false,true);yield return Frames(3);Assert.IsTrue(expected.IsSolid,"Pointer-frame repair failed at "+expected.Cell+"; "+builder.LastAction);}
        private IEnumerator Approach()
        {
            yield return Walk(route.Departure.position);yield return Frames(40);Assert.IsTrue(mission.Sites[0].Surveyed);
            yield return Walk(route.NearBank.position);builder.ToggleCursor();rig.Orbit(180,0);rig.SnapToTarget();builder.TrySelectMaterial("timber");
            foreach(var marker in route.Timber)
            {yield return Click(PaxGrid.Centre(marker.Cell-Vector3Int.right)+new Vector3(0.501f,0,0),marker);yield return Walk(PaxGrid.Centre(marker.Cell)+Vector3.up*0.53f);Assert.That(player.transform.position.y,Is.EqualTo(0).Within(0.08f));}
            yield return Walk(new Vector3(2,0.03f,0));yield return Walk(route.Mooring.position);yield return Frames(40);Assert.IsTrue(mission.Sites[1].Surveyed);
        }
        private IEnumerator Lookout()
        {
            yield return Walk(route.Takeoff.position);rig.ResetView();rig.SnapToTarget();builder.TrySelectMaterial("stone");
            foreach(var marker in route.Stone)yield return Click((Vector3)marker.Cell+new Vector3(0,0.02f,0),marker);
            // Input-frame movement and held jumps, never Warp/teleport on the completion route.
            for(int jump=0;jump<2;jump++)
            {var f=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;var r=Vector3.Cross(Vector3.up,f);player.SetCommand(new Vector2(Vector3.Dot(Vector3.right,r),Vector3.Dot(Vector3.right,f)),true,true);yield return Frames(jump==0?13:34);player.SetCommand(Vector2.zero,false,false);yield return Frames(45);Assert.IsTrue(player.Grounded);Assert.That(player.transform.position.y,Is.EqualTo(jump+1).Within(0.08f));}
            yield return Walk(route.Chart.position);yield return Frames(40);
        }
        [UnityTest]public IEnumerator PhysicalMixedMaterialRouteLeadsToChartAndCompletesWithoutWarp()
        {
            Gather();Assert.IsTrue(save.SaveEdits());yield return Approach();Assert.AreEqual(5,route.TimberCount);yield return Lookout();
            Assert.AreEqual(3,route.StoneCount);Assert.AreEqual(3,mission.SurveyedCount);Assert.AreEqual(0,pile.Count("timber"));Assert.AreEqual(0,pile.Count("stone"));Assert.IsTrue(mission.TryActivate());Assert.AreEqual(PaxExpeditionStage.Complete,mission.Stage);Assert.IsTrue(save.SaveEdits());
            yield return new WaitForSecondsRealtime(0.2f);var root=Object.FindFirstObjectByType<UIDocument>().rootVisualElement;Assert.That(root.Q<Label>("expedition-objective").text,Does.Contain("Ember Reach"));Assert.That(root.Q<Label>("chart-route").text,Does.Contain("5/5"));
        }
        [UnityTest]public IEnumerator WrongMaterialCanBeRecoveredAndSnapshotRetryNeverMintsUnits()
        {
            Gather();player.Warp(route.NearBank.position);yield return Frames(10);var first=route.Timber[0];builder.TrySelectMaterial("stone");Assert.IsTrue(builder.Place(first.Cell));Assert.IsFalse(first.IsSolid);Assert.AreEqual(0,route.TimberCount);Assert.AreEqual(2,pile.Count("stone"));Assert.IsTrue(save.SaveEdits());
            Assert.IsTrue(builder.Mine(first.Cell));Assert.AreEqual(3,pile.Count("stone"));builder.TrySelectMaterial("timber");Assert.IsTrue(builder.Place(first.Cell));Assert.IsTrue(first.IsSolid);Assert.AreEqual(4,pile.Count("timber"));Assert.IsTrue(save.SaveEdits());
            Assert.IsTrue(save.LoadEdits(true));Assert.IsFalse(first.IsSolid);Assert.AreEqual(5,pile.Count("timber"));Assert.AreEqual(2,pile.Count("stone"));
            for(int i=0;i<3;i++){Assert.IsTrue(save.LoadEdits());Assert.IsTrue(first.IsSolid);Assert.AreEqual(4,pile.Count("timber"));Assert.AreEqual(3,pile.Count("stone"));}
            int rev=world.Revision;mission.RestartSurvey();Assert.AreEqual(rev,world.Revision);Assert.AreEqual(4,pile.Count("timber"));Assert.IsTrue(first.IsSolid);Assert.IsFalse(mission.TryActivate());yield return null;
        }
        [UnityTest]public IEnumerator SavedCompletedRouteCanRetryWithoutRespawningSuppliesOrKeepingDiscoveries()
        {
            Gather();yield return Approach();yield return Lookout();Assert.IsTrue(mission.TryActivate());Assert.IsTrue(save.SaveEdits());
            var first=route.Timber[0];Assert.IsTrue(world.TryMine(first.Cell));Assert.IsFalse(first.IsSolid);Assert.IsFalse(save.SaveEdits(),"Unaccounted direct edits must still reject conservation");
            // Move actor to an authored safe landing for load testing only; route test above uses no Warp.
            player.Warp(route.Chart.position);yield return Frames(10);Assert.IsTrue(save.LoadEdits());Assert.IsTrue(first.IsSolid);Assert.AreEqual(0,pile.Count("timber"));
            mission.RestartSurvey();Assert.AreEqual(0,mission.SurveyedCount);Assert.AreEqual(8,mission.RepairCount);Assert.AreEqual(0,pile.Count("stone"));Assert.IsTrue(save.LoadEdits());Assert.AreEqual(0,mission.SurveyedCount);yield return null;
        }
    }
}
