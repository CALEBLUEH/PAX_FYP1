using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxFiniteResourceTests
    {
        private PaxWorld world; private PaxBuilder builder; private PaxPlayer player; private PaxStockpile pile; private PaxPlaytestSave save; private PaxResourceCache cache;
        private string directory;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.captureDeltaTime=1f/60; yield return SceneManager.LoadSceneAsync("PaxResourceExpedition");
            world=Object.FindFirstObjectByType<PaxWorld>(); builder=Object.FindFirstObjectByType<PaxBuilder>(); player=Object.FindFirstObjectByType<PaxPlayer>(); pile=Object.FindFirstObjectByType<PaxStockpile>(); save=Object.FindFirstObjectByType<PaxPlaytestSave>(); cache=Object.FindFirstObjectByType<PaxResourceCache>();
            Object.FindFirstObjectByType<PaxInput>().enabled=false; Object.FindFirstObjectByType<PaxExpeditionInput>().enabled=false;
            directory=Path.Combine(Path.GetTempPath(),"PaxFiniteTests",Guid.NewGuid().ToString("N")); save.UseValidationDirectory(directory);
            for(int i=0;i<15;i++) yield return null; Assert.IsTrue(pile.Ready);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime=0;
            Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(),"PaxFiniteTests")),Path.GetDirectoryName(Path.GetFullPath(directory)));
            if(Directory.Exists(directory)) Directory.Delete(directory,true); yield return null;
        }
        private void Gather() { foreach(var cell in cache.Cells) Assert.IsTrue(builder.Mine(cell), cell.ToString()); }
        private void Build()
        {
            player.Warp(new Vector3(1,0.03f,-3)); Physics.SyncTransforms(); Assert.IsTrue(builder.TrySelectMaterial("stone"));
            foreach(var repair in Object.FindFirstObjectByType<PaxExpedition>().Repairs.OrderBy(r=>r.Cell.z).ThenBy(r=>r.Cell.x)) Assert.IsTrue(builder.Place(repair.Cell));
        }
        [UnityTest] public IEnumerator SuccessfulEditsAccountOnceFailedEditsNeverGainOrSpend()
        {
            builder.TrySelectMaterial("stone"); int rev=world.Revision;
            Assert.AreEqual(PaxActionBlock.InsufficientResources,builder.EvaluatePlacement(new Vector3Int(1,0,-5))); Assert.IsFalse(builder.Place(new Vector3Int(1,0,-5))); Assert.AreEqual(rev,world.Revision);
            Gather(); Assert.AreEqual(6,pile.Count("stone")); Assert.IsFalse(builder.Mine(cache.Cells[0])); Assert.AreEqual(6,pile.Count("stone"));
            Assert.IsFalse(builder.Mine(new Vector3Int(0,-3,-3))); Assert.IsFalse(builder.Mine(new Vector3Int(12,0,12))); Assert.IsFalse(builder.Place(new Vector3Int(0,0,-3))); Assert.IsFalse(builder.Place(new Vector3Int(50,0,50))); Assert.IsFalse(builder.TrySelectMaterial("unknown")); Assert.AreEqual(6,pile.Count("stone"));
            Assert.IsTrue(builder.Place(new Vector3Int(1,0,-5))); Assert.AreEqual(5,pile.Count("stone")); Assert.IsFalse(builder.Place(new Vector3Int(1,0,-5))); Assert.AreEqual(5,pile.Count("stone"));
            Assert.IsTrue(builder.TrySelectMaterial("timber")); Assert.IsTrue(builder.Mine(new Vector3Int(1,0,-5))); Assert.AreEqual(6,pile.Count("stone")); Assert.AreEqual(0,pile.Count("timber")); Assert.IsTrue(save.SaveEdits());
            yield return new WaitForSecondsRealtime(0.2f);
            var ui=Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
            Assert.That(ui.Q<Label>("resource-stockpile").text,Does.Contain("Stone 6/64"));
            Assert.That(ui.Q<Label>("expedition-persistence").text,Does.Contain("stockpile + tiles together"));
        }
        [UnityTest] public IEnumerator CombinedCurrentPreviousRepeatedLoadsRestoreExactStocksAndTiles()
        {
            Gather(); Assert.IsTrue(save.SaveEdits()); Build(); yield return null; Assert.AreEqual(0,pile.Count("stone")); Assert.IsTrue(save.SaveEdits());
            Assert.IsTrue(save.LoadEdits(true)); Assert.AreEqual(6,pile.Count("stone")); Assert.IsFalse(world.Contains(new Vector3Int(1,0,-5)));
            for(int i=0;i<3;i++) { Assert.IsTrue(save.LoadEdits()); Assert.AreEqual(0,pile.Count("stone")); Assert.IsTrue(world.Contains(new Vector3Int(1,0,-5))); }
            Assert.IsTrue(builder.Mine(new Vector3Int(1,0,-5))); Assert.AreEqual(1,pile.Count("stone")); Assert.IsTrue(save.LoadEdits()); Assert.AreEqual(0,pile.Count("stone"));
            Assert.IsTrue(save.LoadEdits(true)); Assert.AreEqual(6,pile.Count("stone")); Assert.IsFalse(world.Contains(new Vector3Int(1,0,-5))); yield return null;
        }
        [UnityTest] public IEnumerator InvalidCountsWorldRulesLedgerTileOnlyAndBodyOverlapLeaveBothStatesIntact()
        {
            Gather(); var original=pile.Capture(); Assert.IsTrue(save.SaveEdits()); int rev=world.Revision;
            foreach(string mode in new[]{"negative","overflow","unknown","mint","world","rules","ledger","overlap"})
            {
                var data=PaxResourceSnapshot.Decode(original.Encode());
                if(mode=="negative") data.Counts["stone"]=-1;
                if(mode=="overflow") data.Counts["stone"]=int.MaxValue;
                if(mode=="unknown") { data.Counts.Remove("earth"); data.Counts.Add("unknown",0); }
                if(mode=="mint") data.Counts["stone"]=7;
                if(mode=="world") data.Tiles.World=new string('0',64);
                if(mode=="rules") data.Rules=new string('0',64);
                if(mode=="ledger") data.Harvested.Add(new Vector3Int(100,100,100));
                if(mode=="overlap") { var cell=new Vector3Int(0,0,-3); data.Tiles.Placed.Add(cell); data.Tiles.Materials[cell]="stone"; data.Counts["stone"]=5; }
                File.WriteAllText(save.SavePath,data.Encode()); Assert.IsFalse(save.LoadEdits(),mode); Assert.AreEqual(6,pile.Count("stone"),mode); Assert.AreEqual(rev,world.Revision,mode);
            }
            File.WriteAllText(save.SavePath,PaxSaveFile.Encode(world.CaptureEdits())); Assert.IsFalse(save.LoadEdits()); Assert.AreEqual(6,pile.Count("stone")); Assert.AreEqual(rev,world.Revision); yield return null;
        }
        [UnityTest] public IEnumerator IncompatiblePrimaryNeverReplacesValidCombinedBackup()
        {
            Gather(); Assert.IsTrue(save.SaveEdits()); Build(); yield return null; Assert.IsTrue(save.SaveEdits());
            string backup=File.ReadAllText(save.BackupPath); File.WriteAllText(save.SavePath,"<corrupt"); Assert.IsFalse(save.LoadEdits()); Assert.AreEqual(0,pile.Count("stone"));
            Assert.IsTrue(save.SaveEdits()); Assert.AreEqual(backup,File.ReadAllText(save.BackupPath)); Assert.AreEqual(1,Directory.GetFiles(directory,"*.invalid-*").Length);
            Assert.IsTrue(save.LoadEdits(true)); Assert.AreEqual(6,pile.Count("stone")); Assert.IsFalse(world.Contains(new Vector3Int(1,0,-5))); yield return null;
        }
        [UnityTest] public IEnumerator AuthoredHigherYieldCannotBeFarmedByRebuildingSameTileAndLimitsBlockMining()
        {
            var entry=pile.Rules.Entries.Single(e=>e.id=="stone"); int oldYield=entry.authoredYield,oldLimit=entry.limit;
            try
            {
                entry.authoredYield=2; entry.limit=2; var cell=cache.Cells[0]; Assert.IsTrue(builder.Mine(cell)); Assert.AreEqual(2,pile.Count("stone"));
                Assert.AreEqual(PaxActionBlock.StockpileFull,builder.EvaluateMining(cache.Cells[1])); Assert.IsFalse(builder.Mine(cache.Cells[1])); Assert.IsTrue(world.Contains(cache.Cells[1]));
                builder.TrySelectMaterial("stone"); Assert.IsTrue(builder.Place(cell)); Assert.AreEqual(1,pile.Count("stone")); Assert.IsTrue(save.SaveEdits());
                Assert.IsTrue(builder.Mine(cell)); Assert.AreEqual(2,pile.Count("stone")); Assert.IsTrue(save.SaveEdits()); Assert.IsTrue(save.LoadEdits(true)); Assert.AreEqual(1,pile.Count("stone")); Assert.IsTrue(world.Contains(cell));
            }
            finally { entry.authoredYield=oldYield; entry.limit=oldLimit; }
            yield return null;
        }
        [UnityTest] public IEnumerator UnmappedMaterialsAndUnaccountedWorldEditsCannotMintOrSaveResources()
        {
            var cell=cache.Cells[0]; Assert.IsTrue(world.TryGet(cell,out var block)); var renderer=block.GetComponent<Renderer>(); var original=renderer.sharedMaterial;
            var material=new Material(Shader.Find("Standard")); int rev=world.Revision;
            try { renderer.sharedMaterial=material; Assert.IsFalse(builder.Mine(cell)); Assert.AreEqual(0,pile.Count("stone")); Assert.AreEqual(rev,world.Revision); Assert.IsFalse(save.SaveEdits()); Assert.IsFalse(File.Exists(save.SavePath)); }
            finally { renderer.sharedMaterial=original; Object.Destroy(material); }
            Assert.IsTrue(world.TryMine(cell)); Assert.IsFalse(save.SaveEdits()); Assert.AreEqual(0,pile.Count("stone")); Assert.IsFalse(File.Exists(save.SavePath)); yield return null;
        }
    }
}
