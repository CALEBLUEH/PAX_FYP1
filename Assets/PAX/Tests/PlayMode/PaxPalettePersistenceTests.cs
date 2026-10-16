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
    public sealed class PaxPalettePersistenceTests
    {
        private PaxWorld world; private PaxBuilder builder; private PaxPlayer player; private PaxMaterialSelection selection; private PaxPlaytestSave session;
        private string directory;
        private static readonly Vector3Int[] Cells = { new Vector3Int(-2,0,-3), new Vector3Int(2,0,-3), new Vector3Int(-1,0,-3), new Vector3Int(1,0,-3) };
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.captureDeltaTime = 1f / 60;
            yield return SceneManager.LoadSceneAsync("PaxPaletteExpedition");
            world = Object.FindFirstObjectByType<PaxWorld>(); builder = Object.FindFirstObjectByType<PaxBuilder>(); player = Object.FindFirstObjectByType<PaxPlayer>();
            selection = Object.FindFirstObjectByType<PaxMaterialSelection>(); session = Object.FindFirstObjectByType<PaxPlaytestSave>();
            Object.FindFirstObjectByType<PaxInput>().enabled = false; Object.FindFirstObjectByType<PaxExpeditionInput>().enabled = false;
            directory = Path.Combine(Path.GetTempPath(), "PaxPaletteTests", Guid.NewGuid().ToString("N")); session.UseValidationDirectory(directory);
            for (int i = 0; i < 15; i++) yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaxPaletteTests")), Path.GetDirectoryName(Path.GetFullPath(directory)));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            yield return null;
        }
        private void BuildMixed()
        { for (int i = 0; i < Cells.Length; i++) { Assert.IsTrue(selection.SelectNumber(i + 2)); Assert.IsTrue(builder.Place(Cells[i])); } Physics.SyncTransforms(); }
        private void CheckMaterial(Vector3Int cell, string id)
        {
            Assert.IsTrue(world.TryGet(cell, out var block)); Assert.IsTrue(world.TryResolveMaterial(id, out var material)); Assert.AreSame(material, block.SurfaceMaterial);
            Assert.IsTrue(block.Solid.enabled); Assert.NotNull(block.SolidShadow);
        }
        [UnityTest] public IEnumerator LegacyFileLoadsGrassMigratesPrimaryAndRetainsLegacyBackupAndSignature()
        {
            string signature = world.SaveSignature;
            Directory.CreateDirectory(directory);
            File.WriteAllText(session.SavePath, File.ReadAllText("Assets/PAX/Tests/Fixtures/LegacyTileEditsV1.xml").Replace(new string('A', 64), signature));
            Assert.IsTrue(session.LoadEdits()); CheckMaterial(Cells[0], "default");
            Assert.IsTrue(session.SaveEdits()); Assert.AreEqual(2, PaxSaveFile.Read(session.SavePath).Version); Assert.AreEqual(1, PaxSaveFile.Read(session.BackupPath).Version);
            Assert.AreEqual("default", PaxSaveFile.Read(session.SavePath).Materials[Cells[0]]);
            Assert.IsTrue(session.LoadEdits(true)); CheckMaterial(Cells[0], "default"); Assert.AreEqual(signature, world.SaveSignature);
            yield return SceneManager.LoadSceneAsync("PaxExpedition"); Assert.AreEqual(signature, Object.FindFirstObjectByType<PaxWorld>().SaveSignature);
        }
        [UnityTest] public IEnumerator MixedMaterialsMineReplacePrimaryAndPreviousRestoreExactly()
        {
            BuildMixed(); yield return null; Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(builder.Mine(Cells[2])); Assert.IsTrue(selection.SelectNumber(5)); Assert.IsTrue(builder.Place(Cells[2])); Assert.IsTrue(session.SaveEdits());
            Assert.IsTrue(session.LoadEdits(true)); CheckMaterial(Cells[0], "timber"); CheckMaterial(Cells[1], "stone"); CheckMaterial(Cells[2], "earth"); CheckMaterial(Cells[3], "roof");
            Assert.IsTrue(session.LoadEdits()); CheckMaterial(Cells[2], "roof"); int revision = world.Revision;
            Assert.IsTrue(session.LoadEdits()); Assert.AreEqual(revision, world.Revision);
            var overlap = world.CaptureEdits(); overlap.Placed.Add(new Vector3Int(0,0,-3)); overlap.Materials[new Vector3Int(0,0,-3)] = "timber";
            Assert.IsFalse(world.TryApplyEdits(overlap, player.BodyBounds, out _)); Assert.AreEqual(revision, world.Revision);
            Assert.IsFalse(builder.Mine(new Vector3Int(0,-3,-3))); yield return null;
        }
        [UnityTest] public IEnumerator UnknownWrongWorldAndInvalidReplacementNeverMutateWorldOrValidFiles()
        {
            BuildMixed(); yield return null; Assert.IsTrue(session.SaveEdits()); Assert.IsTrue(session.SaveEdits());
            var data = world.CaptureEdits(); data.Materials[Cells[0]] = "unregistered";
            string primary = File.ReadAllText(session.SavePath), backup = File.ReadAllText(session.BackupPath); int revision = world.Revision;
            Assert.IsFalse(world.TryApplyEdits(data, player.BodyBounds, out _)); Assert.AreEqual(revision, world.Revision); CheckMaterial(Cells[0], "timber");
            Assert.Catch(() => PaxSaveFile.Write(session.SavePath, data, saved => world.ValidateEdits(saved, out _)));
            Assert.AreEqual(primary, File.ReadAllText(session.SavePath)); Assert.AreEqual(backup, File.ReadAllText(session.BackupPath));
            data.Materials[Cells[0]] = "timber"; data.World = new string('0',64); Assert.IsFalse(world.TryApplyEdits(data, player.BodyBounds, out _)); Assert.AreEqual(revision, world.Revision);
            string incompatible = primary.Replace("material=\"timber\"", "material=\"unregistered\""); Assert.AreNotEqual(primary, incompatible);
            File.WriteAllText(session.SavePath, incompatible); Assert.IsFalse(session.LoadEdits()); Assert.AreEqual(revision, world.Revision);
            Assert.IsTrue(session.SaveEdits()); Assert.AreEqual(backup, File.ReadAllText(session.BackupPath)); Assert.AreEqual(incompatible, File.ReadAllText(Directory.GetFiles(directory, "*.invalid-*").Single()));
            Assert.IsTrue(session.LoadEdits(true)); CheckMaterial(Cells[0], "timber"); yield return null;
        }
        [UnityTest] public IEnumerator NumberSelectionPreviewValidityAndInvalidSelectionKeepAuthoritativeState()
        {
            Assert.IsTrue(selection.SelectNumber(2)); int revision = world.Revision; var material = selection.PreviewSample.sharedMaterial;
            Assert.IsFalse(selection.SelectNumber(9)); Assert.IsFalse(builder.TrySelectMaterial("../bad")); Assert.AreEqual("timber", builder.SelectedMaterialId); Assert.AreSame(material, selection.PreviewSample.sharedMaterial); Assert.AreEqual(revision, world.Revision);
            builder.ToggleCursor(); var camera = Object.FindFirstObjectByType<PaxCamera>().View;
            builder.SetPointer(camera.WorldToScreenPoint(PaxGrid.Centre(new Vector3Int(2,-1,-3))), false, false);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(builder.CanPlace); Assert.IsTrue(builder.PlacementUsesValidMaterial); Assert.AreSame(material, selection.PreviewSample.sharedMaterial);
            builder.SetPointer(camera.WorldToScreenPoint(player.transform.position + Vector3.up * 0.02f), false, false);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(builder.CanPlace); Assert.IsFalse(builder.PlacementUsesValidMaterial);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Label>("construction-palette").text, Does.Contain("Timber [selected]"));
        }
    }
}
