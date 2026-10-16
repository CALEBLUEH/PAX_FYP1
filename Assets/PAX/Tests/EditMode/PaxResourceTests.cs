using System;
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
    public sealed class PaxResourceTests
    {
        private static PaxResourceSnapshot Data()
        {
            var data = new PaxResourceSnapshot { Rules = new string('A',64), Tiles = new PaxTileEdits { World = new string('B',64) } };
            data.Counts.Add("stone", 6); data.Harvested.Add(new Vector3Int(0,0,-1)); return data;
        }
        [Test] public void FullEnvelopeRoundTripsAndTileOnlyFormatsAreRejected()
        {
            var decoded = PaxResourceSnapshot.Decode(Data().Encode()); Assert.AreEqual(6, decoded.Counts["stone"]); Assert.AreEqual(1, decoded.Harvested.Count);
            Assert.Catch(() => PaxResourceSnapshot.Decode(PaxSaveFile.Encode(Data().Tiles)));
            var old = Data().Tiles; old.Version = 1; Assert.Catch(() => PaxResourceSnapshot.Decode(PaxSaveFile.Encode(old)));
        }
        [TestCase("count=\"6\"", "count=\"-1\"")]
        [TestCase("count=\"6\"", "count=\"2147483648\"")]
        [TestCase("count=\"6\"", "count=\"10001\"")]
        [TestCase("id=\"stone\"", "id=\"../stone\"")]
        [TestCase("version=\"1\"", "version=\"99\"")]
        [TestCase("</stockpile>", "<item id=\"stone\" count=\"1\" /></stockpile>")]
        [TestCase("</harvested>", "<cell x=\"0\" y=\"0\" z=\"-1\" /></harvested>")]
        [TestCase("<stockpile>", "<stockpile extra=\"x\">")]
        public void MalformedNegativeOverflowDuplicateAndStaleFormatsReject(string from, string to)
        { string text = Data().Encode(); Assert.AreNotEqual(text, text.Replace(from,to)); Assert.Catch(() => PaxResourceSnapshot.Decode(text.Replace(from,to))); }
        [Test] public void ResourceSceneIsWiredWithSixNodesFiniteUiAndIdempotentAuthoring()
        {
            string[] paths = { PaxResourceAuthoring.ScenePath, PaxPaletteAuthoring.ScenePath, PaxExpeditionAuthoring.ScenePath, PaxIslandAuthoring.ScenePath, PaxPrototypeAuthoring.ScenePath, "ProjectSettings/EditorBuildSettings.asset" };
            var before = paths.Select(File.ReadAllBytes).ToArray(); PaxResourceAuthoring.CreateResourceExpedition();
            for (int i=0; i<paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]));
            var scene = EditorSceneManager.OpenScene(PaxResourceAuthoring.ScenePath, OpenSceneMode.Single);
            var cache = Object.FindFirstObjectByType<PaxResourceCache>(); Assert.AreEqual(6, cache.Cells.Distinct().Count());
            var pile = Object.FindFirstObjectByType<PaxStockpile>(); Assert.IsTrue(pile.Rules.Validate(pile.Palette, out _));
            Assert.That(Path.GetFileName(Object.FindFirstObjectByType<PaxPlaytestSave>().SavePath), Does.StartWith("pax-resources-"));
            Assert.NotNull(Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree().Q<Label>("resource-stockpile"));
            foreach (var root in scene.GetRootGameObjects()) foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.NotNull(component); if (!component.GetType().Namespace.StartsWith("Pax")) continue;
                var property = new SerializedObject(component).GetIterator();
                while(property.NextVisible(true)) if(property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script") Assert.NotNull(property.objectReferenceValue, component.GetType().Name + "." + property.propertyPath);
            }
        }
    }
}
