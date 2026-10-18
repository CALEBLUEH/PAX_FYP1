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
    public sealed class PaxPaletteTests
    {
        private const string Fixture = "Assets/PAX/Tests/Fixtures/LegacyTileEditsV1.xml";
        [Test]
        public void NonDefaultWritesRequireRegistryValidationBeforeCreatingAnyFile()
        {
            var data = PaxSaveFile.Decode(File.ReadAllText(Fixture)); data.Version = 2; data.Materials[data.Placed[0]] = "unregistered";
            string path = Path.Combine(Path.GetTempPath(), "PaxPaletteRejected-" + Guid.NewGuid().ToString("N"), "save.xml");
            Assert.Catch(() => PaxSaveFile.Write(path, data)); Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(path)));
        }
        [Test]
        public void RealLegacyFixtureMigratesToExplicitDefaultIds()
        {
            var old = PaxSaveFile.Decode(File.ReadAllText(Fixture));
            Assert.AreEqual(1, old.Version); Assert.AreEqual(0, old.Materials.Count); Assert.AreEqual(new Vector3Int(-2, 0, -3), old.Placed.Single());
            old.Version = 2;
            var migrated = PaxSaveFile.Decode(PaxSaveFile.Encode(old));
            Assert.AreEqual("default", migrated.Materials[old.Placed[0]]);
        }
        [TestCase("")] [TestCase("../Roof.mat")] [TestCase("Stone")] [TestCase(" stone")] [TestCase("abcdefghijklmnopqrstuvwxyz1234567")]
        public void MalformedIdsAreRejected(string id)
        {
            var data = PaxSaveFile.Decode(File.ReadAllText(Fixture)); data.Version = 2; data.Materials[data.Placed[0]] = id;
            Assert.Catch(() => PaxSaveFile.Decode(PaxSaveFile.Encode(data)));
        }
        [Test]
        public void MissingDuplicateUnknownAttributesAndLegacyMaterialMetadataAreRejected()
        {
            string old = File.ReadAllText(Fixture);
            Assert.Catch(() => PaxSaveFile.Decode(old.Replace("z=\"-3\"", "z=\"-3\" material=\"timber\"")));
            Assert.Catch(() => PaxSaveFile.Decode(old.Replace("version=\"1\"", "version=\"2\"")));
            Assert.Catch(() => PaxSaveFile.Decode(old.Replace("</placed>", "<cell x=\"-2\" y=\"0\" z=\"-3\" /></placed>")));
            var data = PaxSaveFile.Decode(old); data.Version = 2; data.Materials[data.Placed[0]] = "timber";
            string valid = PaxSaveFile.Encode(data);
            Assert.Catch(() => PaxSaveFile.Decode(valid.Replace("material=\"timber\"", "material=\"timber\" material=\"stone\"")));
            data.Materials[Vector3Int.one] = "stone"; Assert.Catch(() => PaxSaveFile.Encode(data));
        }
        [Test]
        public void PaletteRejectsDuplicateIdsReferencesAndInvalidDefaults()
        {
            var source = AssetDatabase.LoadAssetAtPath<PaxMaterialPalette>(PaxPaletteAuthoring.PalettePath);
            var palette = Object.Instantiate(source);
            try
            {
                var entries = source.Entries.Select(entry => new PaxMaterialPalette.Entry { id = entry.id, title = entry.title, material = entry.material }).ToArray();
                entries[1].id = "default"; palette.Configure(entries); Assert.IsFalse(palette.Validate(out _));
                entries[1].id = "timber"; entries[1].material = entries[0].material; Assert.IsFalse(palette.Validate(out _));
                entries[1].material = source.Entries[1].material; entries[0].id = "renamed"; Assert.IsFalse(palette.Validate(out _));
                entries[0].id = "default";
                var incompatibleMaterial = new Material(Shader.Find("Standard"));
                try { entries[1].material = incompatibleMaterial; Assert.IsFalse(palette.Validate(out _)); }
                finally { Object.DestroyImmediate(incompatibleMaterial); }
            }
            finally { Object.DestroyImmediate(palette); }
        }
        [Test]
        public void SavedPaletteSceneHasExplicitReferencesPreviewAndIdempotentAuthoring()
        {
            string[] paths = { PaxPaletteAuthoring.ScenePath, PaxExpeditionAuthoring.ScenePath, PaxIslandAuthoring.ScenePath, PaxPrototypeAuthoring.ScenePath, "ProjectSettings/EditorBuildSettings.asset" };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            PaxPaletteAuthoring.CreatePaletteExpedition();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]));
            var scene = EditorSceneManager.OpenScene(PaxPaletteAuthoring.ScenePath, OpenSceneMode.Single);
            var selection = Object.FindFirstObjectByType<PaxMaterialSelection>(); Assert.NotNull(selection);
            Assert.IsTrue(selection.Palette.Validate(out _)); Assert.AreEqual(5, selection.Palette.Entries.Count);
            Assert.IsNull(selection.PreviewSample.GetComponent<Collider>()); Assert.AreEqual(2, selection.PreviewSample.gameObject.layer);
            Assert.NotNull(Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree().Q<Label>("construction-palette"));
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.NotNull(component);
                if (!component.GetType().Namespace.StartsWith("Pax")) continue;
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.name != "pause") Assert.NotNull(property.objectReferenceValue, component.GetType().Name + "." + property.propertyPath);
            }
        }
    }
}
