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
    public sealed class PaxEntryAuthoringTests
    {
        [Test] public void AuthoredEntryAndBothReturnPathsAreWiredAndTruthful()
        {
            EditorSceneManager.OpenScene(PaxEntryAuthoring.ScenePath, OpenSceneMode.Single);
            Assert.AreEqual(1, Object.FindObjectsByType<PaxExpeditionSelection>(FindObjectsSortMode.None).Length); Assert.IsNull(Object.FindFirstObjectByType<PaxPause>());
            var selection = Object.FindFirstObjectByType<PaxExpeditionSelection>(); Assert.AreEqual(PaxResourceAuthoring.ScenePath, selection.ResourceScenePath); Assert.AreEqual(PaxChartAuthoring.ScenePath, selection.ChartScenePath);
            var ui = Object.FindFirstObjectByType<UIDocument>(); Assert.NotNull(ui.panelSettings); Assert.NotNull(ui.visualTreeAsset); Assert.NotNull(Camera.main);
            var tree = ui.visualTreeAsset.CloneTree(); Assert.AreEqual(6, tree.Query<Button>().ToList().Count); Assert.NotNull(tree.Q<Button>("select-resource")); Assert.NotNull(tree.Q<Button>("select-chart")); Assert.NotNull(tree.Q<Button>("select-ember"));
            string text = string.Join(" ", tree.Query<Label>().ToList().Select(label => label.text));
            foreach (string word in new[] { "F9", "F5", "F8", "F6", "session-only", "nothing loads automatically", "never overwrite saved files" }) Assert.That(text, Does.Contain(word));
            foreach (string path in new[] { PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath })
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single); Assert.AreEqual(PaxEntryAuthoring.ScenePath, Object.FindFirstObjectByType<PaxPause>().SelectionScenePath);
                tree = Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree(); foreach (string button in new[] { "pause-selection", "return-cancel", "return-confirm" }) Assert.AreEqual(1, tree.Query<Button>(button).ToList().Count);
            }
            Assert.IsTrue(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == PaxEntryAuthoring.ScenePath));
        }
        [Test] public void EntryAuthoringIsByteIdempotentAcrossAllScenesAndUi()
        {
            string[] paths = { PaxEntryAuthoring.ScenePath, PaxEntryAuthoring.UiPath, PaxPrototypeAuthoring.ScenePath, PaxIslandAuthoring.ScenePath, PaxExpeditionAuthoring.ScenePath, PaxPaletteAuthoring.ScenePath, PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath, PaxResourceAuthoring.UiPath, PaxChartAuthoring.UiPath, PaxPauseAuthoring.HelpPath, "ProjectSettings/EditorBuildSettings.asset" };
            var before = paths.Select(File.ReadAllBytes).ToArray(); PaxEntryAuthoring.EnsureEntry(); PaxEntryAuthoring.EnsureEntry();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
        }
    }
}
