using System.IO;
using System.Linq;
using NUnit.Framework;
using Pax.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxContinueAuthoringTests
    {
        [Test] public void CatalogExactlyMirrorsThreeAuthoredScenesAndTheirSlots()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PaxContinueCatalog>(PaxContinueAuthoring.CatalogPath); Assert.AreEqual(3, catalog.Slots.Count); Assert.AreEqual(3, catalog.Slots.Select(slot => slot.file).Distinct().Count());
            foreach (var slot in catalog.Slots)
            {
                EditorSceneManager.OpenScene(slot.scenePath, OpenSceneMode.Single); var world = Object.FindFirstObjectByType<PaxWorld>(); var stocks = Object.FindFirstObjectByType<PaxStockpile>(); var storage = Object.FindFirstObjectByType<PaxPlaytestSave>();
                Assert.AreEqual(world.Minimum, slot.minimum); Assert.AreEqual(world.Maximum, slot.maximum); Assert.AreEqual(world.Blocks.Count, slot.tiles.Length); Assert.AreEqual(stocks.Rules, slot.rules); Assert.AreEqual(stocks.Palette, slot.palette); Assert.AreEqual(storage.SaveFile, slot.file); Assert.AreEqual(storage.SaveFolder, slot.folder);
                foreach (var block in world.Blocks) { Assert.IsTrue(slot.TryGetAuthored(block.Cell, out var material, out bool mineable)); Assert.AreEqual(block.SurfaceMaterial, material); Assert.AreEqual(block.Mineable, mineable); }
                var receiver = Object.FindFirstObjectByType<PaxContinueReceiver>(); Assert.NotNull(receiver); var property = new SerializedObject(receiver).GetIterator(); while (property.NextVisible(true)) if (property.propertyType == SerializedPropertyType.ObjectReference) Assert.NotNull(property.objectReferenceValue, property.propertyPath);
            }
            EditorSceneManager.OpenScene(PaxEntryAuthoring.ScenePath, OpenSceneMode.Single); var selection = Object.FindFirstObjectByType<PaxExpeditionSelection>(); Assert.AreEqual(catalog, selection.ContinueCatalog); var keys = new SerializedObject(selection); Assert.AreEqual((int)KeyCode.Alpha4, keys.FindProperty("continueResourceKey").intValue); Assert.AreEqual((int)KeyCode.Alpha5, keys.FindProperty("continueChartKey").intValue); Assert.AreEqual((int)KeyCode.Alpha6, keys.FindProperty("continueEmberKey").intValue); var document = Object.FindFirstObjectByType<UIDocument>(); Assert.AreEqual(PanelScaleMode.ConstantPixelSize, document.panelSettings.scaleMode); var tree = document.visualTreeAsset.CloneTree();
            foreach (string name in new[] { "resource", "chart", "ember" }) { Assert.NotNull(tree.Q<Button>("select-" + name)); Assert.NotNull(tree.Q<Button>("continue-" + name)); Assert.NotNull(tree.Q<Label>("saved-" + name)); }
            string text = string.Join(" ", tree.Query<Label>().ToList().Select(label => label.text)); foreach (string required in new[] { "session-only", "exact position is not saved", "at camp", "never overwrite saved files", "Spawn clearance" }) Assert.That(text, Does.Contain(required));
            Assert.Throws<System.ArgumentException>(() => PaxPlaytestSave.ResolveSlotPath(Path.GetTempPath(), "..", "bad.xml")); Assert.Throws<System.ArgumentException>(() => PaxPlaytestSave.ResolveSlotPath(Path.GetTempPath(), "safe", "../bad.xml"));
        }
        [Test] public void ContinueAuthoringPreservesSceneCatalogPanelAndUiBytesOnRepeat()
        {
            string[] paths = { PaxPrototypeAuthoring.ScenePath, PaxIslandAuthoring.ScenePath, PaxExpeditionAuthoring.ScenePath, PaxPaletteAuthoring.ScenePath, PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath, PaxEmberAuthoring.ScenePath, PaxEntryAuthoring.ScenePath, PaxContinueAuthoring.CatalogPath, PaxContinueAuthoring.PanelPath, PaxEntryAuthoring.UiPath, "ProjectSettings/EditorBuildSettings.asset" };
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PaxContinueAuthoring.EnsureContinue(); PaxContinueAuthoring.EnsureContinue(); for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]), paths[i]);
        }
    }
}
