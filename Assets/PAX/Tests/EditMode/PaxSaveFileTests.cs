using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Pax.Tests
{
    public sealed class PaxSaveFileTests
    {
        private string directory, path;
        private static readonly string Parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaxSaveFileTests"));
        private static PaxTileEdits Data(int x)
        {
            var data = new PaxTileEdits { World = new string('A', 64) };
            data.Placed.Add(new Vector3Int(x, -2, 3));
            return data;
        }
        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "playtest.xml");
        }
        [TearDown]
        public void Cleanup()
        {
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)), Is.EqualTo(Parent));
            Directory.Delete(directory, true);
        }
        [Test]
        public void TileDataRoundTripsWithoutSceneReferences()
        {
            var data = Data(-12);
            data.Removed.Add(new Vector3Int(2, 3, -4));
            var decoded = PaxSaveFile.Decode(PaxSaveFile.Encode(data));
            Assert.That(decoded.World, Is.EqualTo(data.World));
            CollectionAssert.AreEqual(data.Placed, decoded.Placed);
            CollectionAssert.AreEqual(data.Removed, decoded.Removed);
        }
        [TestCase("")]
        [TestCase("<broken")]
        [TestCase("<other />")]
        [TestCase("<!DOCTYPE x [<!ENTITY y 'invalid'>]><x>&y;</x>")]
        public void EmptyMalformedAndEntityDocumentsAreRejected(string input)
        {
            var error = Assert.Catch(() => PaxSaveFile.Decode(input));
            Assert.IsTrue(PaxSaveFile.IsExpectedFailure(error));
        }
        [TestCase("version=\"1\"", "version=\"2\"")]
        [TestCase("<removed />", "<removed /><removed />")]
        [TestCase("<removed />", "<unknown />")]
        [TestCase("x=\"1\"", "x=\"1.5\"")]
        [TestCase("x=\"1\"", "x=\"1\" extra=\"unknown\"")]
        public void UnsupportedOrUnknownDataIsRejectedRatherThanDiscarded(string original, string replacement)
        {
            string input = PaxSaveFile.Encode(Data(1)).Replace(original, replacement);
            Assert.That(input, Is.Not.EqualTo(PaxSaveFile.Encode(Data(1))), "Fixture must actually alter the document");
            var error = Assert.Catch(() => PaxSaveFile.Decode(input));
            Assert.IsTrue(PaxSaveFile.IsExpectedFailure(error));
        }
        [Test]
        public void PreviousValidSaveSurvivesASecondSaveAndCorruptPrimaryRepair()
        {
            PaxSaveFile.Write(path, Data(1));
            PaxSaveFile.Write(path, Data(2));
            Assert.That(PaxSaveFile.Read(path).Placed[0].x, Is.EqualTo(2));
            Assert.That(PaxSaveFile.Read(path + ".bak").Placed[0].x, Is.EqualTo(1));
            File.WriteAllText(path, "<corrupt");
            PaxSaveFile.Write(path, Data(3));
            Assert.That(PaxSaveFile.Read(path).Placed[0].x, Is.EqualTo(3));
            Assert.That(PaxSaveFile.Read(path + ".bak").Placed[0].x, Is.EqualTo(1));
            var rejected = Directory.GetFiles(directory, "*.invalid-*");
            Assert.That(rejected.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(rejected[0]), Is.EqualTo("<corrupt"));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp-*"));
        }
        [Test]
        public void InvalidReplacementDoesNotChangeAnExistingSave()
        {
            PaxSaveFile.Write(path, Data(1));
            string before = File.ReadAllText(path);
            var invalid = Data(2); invalid.Version = 500;
            Assert.Catch(() => PaxSaveFile.Write(path, invalid));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }
        [Test]
        public void OversizedInputIsRejectedBeforeParsing()
        {
            Assert.Catch(() => PaxSaveFile.Decode(new string(' ', PaxSaveFile.MaximumBytes + 1)));
            var data = Data(1);
            data.Placed.AddRange(Enumerable.Repeat(Vector3Int.zero, PaxSaveFile.MaximumEdits));
            Assert.Catch(() => PaxSaveFile.Decode(PaxSaveFile.Encode(data)));
        }
    }
}
