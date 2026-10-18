using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxFrontierPlayTests
    {
        private string directory; private PaxGeneratedFrontier frontier; private PaxPause pause; private PaxWorld world; private PaxPlayer player; private PaxCamera rig; private PaxStockpile stocks; private PaxBuilder builder; private PaxPlaytestSave save; private PaxInput input;
        private static IEnumerator Frames(int count) { for(int i=0;i<count;i++) yield return null; }
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1; Time.captureDeltaTime=1f/60; directory=Path.Combine(Path.GetTempPath(),"PaxFrontierTests",Guid.NewGuid().ToString("N"));
            yield return SceneManager.LoadSceneAsync("PaxGeneratedFrontier"); frontier=Object.FindFirstObjectByType<PaxGeneratedFrontier>(); pause=Object.FindFirstObjectByType<PaxPause>(); pause.UseValidationInput(); input=Object.FindFirstObjectByType<PaxInput>(); input.enabled=false; Object.FindFirstObjectByType<PaxExpeditionInput>().enabled=false;
            world=Object.FindFirstObjectByType<PaxWorld>(); player=Object.FindFirstObjectByType<PaxPlayer>(); rig=Object.FindFirstObjectByType<PaxCamera>(); stocks=Object.FindFirstObjectByType<PaxStockpile>(); builder=Object.FindFirstObjectByType<PaxBuilder>(); save=Object.FindFirstObjectByType<PaxPlaytestSave>(); save.UseValidationDirectory(directory); yield return Release(); yield return Frames(15);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pause!=null)pause.enabled=false; Time.timeScale=1; Time.captureDeltaTime=0;
            if(Directory.Exists(directory)) { Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(),"PaxFrontierTests")),Path.GetDirectoryName(Path.GetFullPath(directory))); Directory.Delete(directory,true); } yield return null;
        }
        private IEnumerator Release() { for(int i=0;i<4;i++){pause.ProcessInput(false,false);yield return null;} }
        [UnityTest] public IEnumerator SavedLayoutUsesExistingSignatureAndDefinitionEditsNeverRegenerate()
        {
            Assert.IsTrue(stocks.Ready && player.Grounded); Assert.AreEqual(0,world.CaptureEdits().Placed.Count); Assert.AreEqual(0,stocks.Count("timber"));
            var layout=PaxFrontierLayout.Generate(frontier.Definition); var materials=world.Blocks.GroupBy(b=>b.SurfaceMaterial.name).ToDictionary(g=>g.Key,g=>g.First().SurfaceMaterial);
            var expected=new PaxContinueSlot {minimum=layout.Minimum,maximum=layout.Maximum,tiles=layout.Tiles.Select(t=>new PaxContinueSlot.Tile {cell=t.Cell,material=materials[t.Material.ToString()],mineable=t.Mineable}).ToArray()};
            Assert.AreEqual(expected.SaveSignature,world.SaveSignature); string signature=world.SaveSignature; int count=world.Blocks.Count, seed=frontier.Definition.terrain.seed;
            try {frontier.Definition.terrain.seed++;yield return Frames(5);Assert.AreEqual(signature,world.SaveSignature);Assert.AreEqual(count,world.Blocks.Count);Assert.AreEqual(seed,frontier.AuthoredSeed);}
            finally {frontier.Definition.terrain.seed=seed;}
            Assert.That(save.SaveFile,Does.StartWith("pax-frontier-")); Assert.That(save.SaveFile,Does.Not.StartWith("pax-resources-"));
        }
        private IEnumerator Walk(Vector3 target)
        {
            for(int i=0;i<500;i++){var delta=target-player.transform.position;delta.y=0;if(delta.magnitude<0.09f){player.ClearCommand();yield break;}var f=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;var r=Vector3.Cross(Vector3.up,f);input.ApplyFrame(new PaxInputFrame {movement=new Vector2(Vector3.Dot(delta.normalized,r),Vector3.Dot(delta.normalized,f))});yield return null;} Assert.Fail("Normal walk blocked: "+target);
        }
        [UnityTest] public IEnumerator EarnedGeneratedResourceRoundTripKeepsHarvestHistoryAndRejectsForgedBudget()
        {
            var house=frontier.Structures.Single(s=>s.kind=="House");yield return Walk(new Vector3(0,0.03f,house.door.z-2));yield return Walk(new Vector3(house.centre.x,0.03f,house.door.z-2));var cell=frontier.Cache.Cells.First(c=>c.x<0);
            Assert.IsTrue(builder.Mine(cell));Assert.AreEqual(1,stocks.Count("timber"));input.ApplyFrame(new PaxInputFrame {save=true});Assert.IsTrue(File.Exists(save.SavePath));
            Assert.IsTrue(builder.TrySelectMaterial("timber"));Assert.IsTrue(builder.Place(cell));input.ApplyFrame(new PaxInputFrame {save=true});string expected=stocks.Capture().Encode();Assert.AreEqual(0,stocks.Count("timber"));
            for(int i=0;i<3;i++){Assert.IsTrue(builder.Mine(cell));Assert.AreEqual(1,stocks.Count("timber"));input.ApplyFrame(new PaxInputFrame {load=true});yield return Frames(2);Assert.AreEqual(expected,stocks.Capture().Encode());}
            Assert.AreEqual(1,stocks.Capture().Harvested.Count);var forged=stocks.Capture();forged.Counts["timber"]++;Assert.IsFalse(stocks.Validate(forged,out _));Assert.AreEqual(expected,stocks.Capture().Encode());
            input.ApplyFrame(new PaxInputFrame {previous=true});yield return Frames(2);Assert.AreEqual(1,stocks.Count("timber"));Assert.IsFalse(world.Contains(cell));Assert.AreEqual(1,stocks.Capture().Harvested.Count);
            string signature=world.SaveSignature;yield return SceneManager.LoadSceneAsync("PaxResourceExpedition");var other=Object.FindFirstObjectByType<PaxStockpile>();Object.FindFirstObjectByType<PaxInput>().enabled=false;yield return Frames(3);Assert.AreNotEqual(signature,Object.FindFirstObjectByType<PaxWorld>().SaveSignature);Assert.IsFalse(other.Validate(PaxResourceSnapshot.Decode(expected),out _));
        }
        [UnityTest] public IEnumerator FocusPauseRequiresResumeAndBlocksGeneratedWorldSaveAndHeldMovement()
        {
            pause.ProcessFocus(false);var position=player.transform.position;input.ApplyFrame(new PaxInputFrame {movement=Vector2.one,save=true,jumpPressed=true,jumpHeld=true});yield return Frames(5);Assert.AreEqual(position,player.transform.position);Assert.IsFalse(File.Exists(save.SavePath));Assert.AreEqual(0,Time.timeScale);
            pause.ProcessFocus(true);yield return Release();Assert.IsTrue(pause.IsPaused);pause.Resume();pause.ProcessInput(false,true);input.ApplyFrame(new PaxInputFrame {movement=Vector2.one,save=true});yield return Frames(3);Assert.AreEqual(position,player.transform.position);Assert.IsFalse(File.Exists(save.SavePath));yield return Release();Assert.IsFalse(pause.BlocksActions);Assert.AreEqual(1,Time.timeScale);
        }
    }
}
