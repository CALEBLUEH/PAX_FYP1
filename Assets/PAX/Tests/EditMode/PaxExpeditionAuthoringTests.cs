using System.IO;
using System.Linq;
using NUnit.Framework;
using Pax.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax.Tests
{
    public sealed class PaxExpeditionAuthoringTests
    {
        [Test]
        public void SceneHasExplicitWiringEmptySupportedRepairCellsAndUniqueSlot()
        {
            var scene = EditorSceneManager.OpenScene(PaxExpeditionAuthoring.ScenePath, OpenSceneMode.Single);
            var mission = Object.FindFirstObjectByType<PaxExpedition>();
            var world = Object.FindFirstObjectByType<PaxWorld>(); world.Reindex();
            Assert.NotNull(mission); Assert.AreEqual(3, mission.Sites.Length); Assert.AreEqual(6, mission.Repairs.Length);
            Assert.AreEqual(6, mission.Repairs.Select(repair => repair.Cell).Distinct().Count());
            foreach (var repair in mission.Repairs)
            {
                Assert.IsTrue(world.InBounds(repair.Cell)); Assert.IsFalse(world.Contains(repair.Cell)); Assert.IsTrue(world.HasSupport(repair.Cell));
                Assert.IsNull(repair.GetComponent<Collider>(), "Blueprint must not block placement rays or the player");
                Assert.AreEqual(2, repair.gameObject.layer);
            }
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.NotNull(component, "Missing script");
                if (!component.GetType().Namespace.StartsWith("Pax")) continue;
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.name != "pause") Assert.NotNull(property.objectReferenceValue, component.GetType().Name + "." + property.propertyPath);
            }
            var session = Object.FindFirstObjectByType<PaxPlaytestSave>();
            Assert.That(Path.GetFileName(session.SavePath), Does.StartWith("pax-expedition-"));
            Assert.AreEqual("pax-expedition-".Length + 32, Path.GetFileNameWithoutExtension(session.SavePath).Length);
            Assert.IsTrue(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == PaxExpeditionAuthoring.ScenePath));
            var tree = Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree();
            foreach (var name in new[] { "status", "mining", "placement", "save", "expedition-objective", "expedition-persistence", "expedition-sites" }) Assert.NotNull(tree.Q<Label>(name));
            Assert.AreEqual(1, Object.FindObjectsByType<PaxExpedition>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void RepeatedAuthoringKeepsAllScenesAndBuildSettingsByteIdentical()
        {
            string[] paths = { PaxExpeditionAuthoring.ScenePath, PaxIslandAuthoring.ScenePath, PaxPrototypeAuthoring.ScenePath, "ProjectSettings/EditorBuildSettings.asset" };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            PaxExpeditionAuthoring.CreateExpedition();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]));
        }
    }
}
