using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pax.Tests
{
    public sealed class PaxGridTests
    {
        [TestCase(-2, 1f)] [TestCase(-1, 1f)] [TestCase(0, 0.75f)]
        [TestCase(1, 0.5f)] [TestCase(2, 0.25f)] [TestCase(3, 0.1f)] [TestCase(4, 0f)]
        public void HeightLayersMatchDesign(int layer, float expected) => Assert.That(PaxGrid.HeightOpacity(layer), Is.EqualTo(expected));

        [TestCase(-4,-3,-2)] [TestCase(0,0,0)] [TestCase(5,8,2)]
        public void CellCentreRoundTrips(int x, int y, int z)
        {
            var cell = new Vector3Int(x,y,z);
            Assert.That(PaxGrid.Cell(PaxGrid.Centre(cell)),Is.EqualTo(cell));
        }
        [Test]
        public void ReachIsThreeDimensionalAndIncludesBoundary()
        {
            Assert.IsTrue(PaxGrid.InReach(new Vector3(0,0.5f,0),new Vector3Int(3,0,4),5));
            Assert.IsFalse(PaxGrid.InReach(new Vector3(0,0.5f,0),new Vector3Int(3,1,4),5));
        }
        [Test]
        public void AuthoredSceneHasRequiredReferencesAndUniqueCells()
        {
            var scene = EditorSceneManager.OpenScene(Pax.Editor.PaxPrototypeAuthoring.ScenePath,OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());
            var world = Object.FindFirstObjectByType<PaxWorld>();
            Assert.NotNull(world);
            var cells = new System.Collections.Generic.HashSet<Vector3Int>();
            foreach (var block in world.Blocks)
            {
                Assert.NotNull(block);
                Assert.NotNull(block.Solid);
                Assert.NotNull(block.SolidShadow);
                Assert.IsTrue(cells.Add(block.Cell),$"Duplicate {block.Cell}");
            }
            Assert.That(cells.Count,Is.GreaterThan(1000));
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.NotNull(component,"Missing script");
                if (!component.GetType().Namespace.StartsWith("Pax")) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                        Assert.NotNull(property.objectReferenceValue,$"{component.GetType().Name}.{property.propertyPath}");
                }
            }
            Assert.That(System.Array.Exists(EditorBuildSettings.scenes,s => s.enabled && s.path == Pax.Editor.PaxPrototypeAuthoring.ScenePath),Is.True);
            var shader = Shader.Find("PAX/Block Cutaway");
            Assert.NotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
        }
        [Test]
        public void AuthoringIsIdempotentAndPreservesBuildConfig()
        {
            var before = System.IO.File.ReadAllBytes(Pax.Editor.PaxPrototypeAuthoring.ScenePath);
            var settings = System.IO.File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset");
            Pax.Editor.PaxPrototypeAuthoring.CreatePrototype();
            CollectionAssert.AreEqual(before,System.IO.File.ReadAllBytes(Pax.Editor.PaxPrototypeAuthoring.ScenePath));
            CollectionAssert.AreEqual(settings,System.IO.File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset"));
        }
    }
}
