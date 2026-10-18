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
    public sealed class PaxPauseAuthoringTests
    {
        [Test] public void TwoFiniteScenesHaveOneAuthoredPauseAndNoMissingWiring()
        {
            foreach (string path in new[] { PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Assert.AreEqual(1, Object.FindObjectsByType<PaxPause>(FindObjectsSortMode.None).Length);
                var pause = Object.FindFirstObjectByType<PaxPause>();
                var tree = Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree();
                Assert.NotNull(tree.Q("pause-overlay")); Assert.NotNull(tree.Q<Button>("pause-resume")); Assert.NotNull(tree.Q<Label>("pause-mission"));
                Assert.AreEqual(1, tree.Query<Button>("pause-resume").ToList().Count);
                foreach (var root in scene.GetRootGameObjects()) foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    Assert.NotNull(component);
                    if (component.GetType().Namespace != "Pax") continue;
                    var property = new SerializedObject(component).GetIterator();
                    while (property.NextVisible(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script") Assert.NotNull(property.objectReferenceValue, component.GetType().Name + "." + property.propertyPath);
                }
                var text = string.Join(" ", tree.Query<Label>().ToList().Select(label => label.text));
                foreach (string word in new[] { "F5", "F8", "F9", "F6", "precise", "MMB", "Escape", "finite stocks" }) Assert.That(text, Does.Contain(word));
                Assert.That(new SerializedObject(pause).FindProperty("missionHelp").stringValue, Does.Contain(path == PaxChartAuthoring.ScenePath ? "F recovers the chart" : "F activates it"));
            }
        }
        [Test] public void PauseAuthoringIsByteIdempotentAndEarlierScenesStayOptOut()
        {
            string[] earlier = { PaxPrototypeAuthoring.ScenePath, PaxIslandAuthoring.ScenePath, PaxExpeditionAuthoring.ScenePath, PaxPaletteAuthoring.ScenePath };
            string[] paths = earlier.Concat(new[] { PaxResourceAuthoring.ScenePath, PaxChartAuthoring.ScenePath, PaxResourceAuthoring.UiPath, PaxChartAuthoring.UiPath, PaxPauseAuthoring.HelpPath, "ProjectSettings/EditorBuildSettings.asset" }).ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray(); PaxPauseAuthoring.EnsurePause(); PaxPauseAuthoring.EnsurePause();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
            foreach (string path in earlier)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single); Assert.IsNull(Object.FindFirstObjectByType<PaxPause>());
                foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                { if (component.GetType().Namespace != "Pax") continue; var optional = new SerializedObject(component).FindProperty("pause"); if (optional != null) Assert.IsNull(optional.objectReferenceValue); }
            }
        }
    }
}
