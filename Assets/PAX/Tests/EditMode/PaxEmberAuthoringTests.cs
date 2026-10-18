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
    public sealed class PaxEmberAuthoringTests
    {
        [Test] public void EmberHasFiniteTypedRepairsThreeNotesLampsAndDistinctSavedWiring()
        {
            var scene = EditorSceneManager.OpenScene(PaxEmberAuthoring.ScenePath, OpenSceneMode.Single);
            var world = Object.FindFirstObjectByType<PaxWorld>(); world.Reindex(); var route = Object.FindFirstObjectByType<PaxChartRoute>();
            Assert.AreEqual(5, route.Timber.Length); Assert.AreEqual(2, route.Stone.Length); Assert.IsTrue(route.Timber.All(marker => marker.RequiredMaterialId == "timber")); Assert.IsTrue(route.Stone.All(marker => marker.RequiredMaterialId == "stone"));
            Assert.AreEqual(3, Object.FindFirstObjectByType<PaxExpedition>().Sites.Length); Assert.AreEqual(2, Object.FindFirstObjectByType<PaxCompletionLamps>().AdditionalLamps.Length);
            var cache = Object.FindFirstObjectByType<PaxResourceCache>(); Assert.AreEqual(7, cache.Cells.Length);
            foreach (var cell in cache.Cells) { Assert.IsTrue(world.TryGet(cell, out var block)); Assert.IsTrue(block.Mineable); }
            foreach (var block in world.Blocks) { Assert.IsTrue(world.InBounds(block.Cell)); Assert.NotNull(block.Solid); Assert.NotNull(block.SolidShadow); }
            Assert.That(Path.GetFileName(Object.FindFirstObjectByType<PaxPlaytestSave>().SavePath), Does.StartWith("pax-ember-")); Assert.AreEqual(PaxEntryAuthoring.ScenePath, Object.FindFirstObjectByType<PaxPause>().SelectionScenePath);
            foreach (var root in scene.GetRootGameObjects()) foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            { Assert.NotNull(component); if (component.GetType().Namespace != "Pax") continue; var property = new SerializedObject(component).GetIterator(); while (property.NextVisible(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script") Assert.NotNull(property.objectReferenceValue, component.GetType().Name + "." + property.propertyPath); }
            var tree = Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree(); Assert.IsTrue(tree.Query<Label>().ToList().Any(label => label.text.Contains("F Rekindle lamps")));
            EditorSceneManager.OpenScene(PaxEntryAuthoring.ScenePath, OpenSceneMode.Single); Assert.AreEqual(PaxEmberAuthoring.ScenePath, Object.FindFirstObjectByType<PaxExpeditionSelection>().EmberScenePath); Assert.IsTrue(EditorBuildSettings.scenes.Any(item => item.enabled && item.path == PaxEmberAuthoring.ScenePath));
        }
        [Test] public void EmberAuthoringPreservesAllEarlierContentAndIsByteIdempotent()
        {
            string[] paths = { PaxPrototypeAuthoring.ScenePath, PaxIslandAuthoring.ScenePath, PaxExpeditionAuthoring.ScenePath, PaxPaletteAuthoring.ScenePath, PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath, PaxEntryAuthoring.ScenePath, PaxEmberAuthoring.ScenePath, PaxResourceAuthoring.UiPath, PaxChartAuthoring.UiPath, PaxEmberAuthoring.UiPath, PaxEntryAuthoring.UiPath, "ProjectSettings/EditorBuildSettings.asset" };
            var before = paths.Select(File.ReadAllBytes).ToArray(); PaxEmberAuthoring.EnsureEmber(); PaxEmberAuthoring.EnsureEmber();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
        }
    }
}
