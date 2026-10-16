using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Pax
{
    public static class PaxSaveFile
    {
        public const int MaximumBytes = 1024 * 1024;
        public const int MaximumEdits = 20000;

        public static string Encode(PaxTileEdits data)
        {
            var root = new XElement("pax-playtest", new XAttribute("version", data.Version), new XAttribute("world", data.World));
            foreach (var pair in new[] { ("removed", data.Removed), ("placed", data.Placed) })
            {
                var group = new XElement(pair.Item1);
                foreach (var cell in pair.Item2)
                    group.Add(new XElement("cell", new XAttribute("x", cell.x), new XAttribute("y", cell.y), new XAttribute("z", cell.z)));
                root.Add(group);
            }
            return new XDocument(root).ToString();
        }

        public static PaxTileEdits Decode(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > MaximumBytes)
                throw new InvalidDataException("Save is empty or exceeds the one-megabyte playtest limit.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes };
            using (var input = new StringReader(text))
            using (var reader = XmlReader.Create(input, settings))
            {
                var root = XDocument.Load(reader).Root;
                if (root == null || root.Name != "pax-playtest") throw new InvalidDataException("Not a PAX playtest save.");
                RequireAttributes(root, "version", "world");
                if (!int.TryParse((string)root.Attribute("version"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) || version != PaxTileEdits.CurrentVersion)
                    throw new InvalidDataException("Unsupported save version. The current world was kept.");
                string world = (string)root.Attribute("world");
                if (world == null || world.Length != 64 || world.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Save world signature is missing or invalid.");
                var groups = root.Elements().ToArray();
                if (groups.Length != 2 || groups.Count(e => e.Name == "removed") != 1 || groups.Count(e => e.Name == "placed") != 1 || HasDataText(root))
                    throw new InvalidDataException("Save contains missing, repeated or unknown sections.");
                var data = new PaxTileEdits { Version = version, World = world };
                foreach (var group in groups)
                {
                    if (group.HasAttributes || HasDataText(group)) throw new InvalidDataException("Invalid tile section.");
                    var destination = group.Name == "removed" ? data.Removed : data.Placed;
                    foreach (var cell in group.Elements())
                    {
                        if (cell.Name != "cell" || cell.HasElements || HasDataText(cell)) throw new InvalidDataException("Unknown tile data. Save was not applied.");
                        RequireAttributes(cell, "x", "y", "z");
                        destination.Add(new Vector3Int(ReadInt(cell, "x"), ReadInt(cell, "y"), ReadInt(cell, "z")));
                        if (data.Removed.Count + data.Placed.Count > MaximumEdits) throw new InvalidDataException("Too many tile edits in this save.");
                    }
                }
                return data;
            }
        }

        private static bool HasDataText(XElement element) => element.Nodes().Any(n => n is XText text && !string.IsNullOrWhiteSpace(text.Value));
        private static int ReadInt(XElement cell, string axis)
        {
            if (!int.TryParse((string)cell.Attribute(axis), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw new InvalidDataException("Tile coordinates must be whole numbers.");
            return value;
        }
        private static void RequireAttributes(XElement element, params string[] names)
        {
            if (element.Attributes().Count() != names.Length || element.Attributes().Any(a => !names.Any(n => a.Name == n)))
                throw new InvalidDataException("Save contains missing or unknown attributes.");
        }

        public static PaxTileEdits Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("No playtest save exists yet. Press Save first.", path);
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Save exceeds the playtest size limit.");
            return Decode(File.ReadAllText(path, Encoding.UTF8));
        }

        public static void Write(string path, PaxTileEdits data, Func<PaxTileEdits, bool> compatible = null)
        {
            string content = Encode(data);
            var normalized = Decode(content); // Never replace a recoverable file with invalid data.
            if (compatible != null && !compatible(normalized)) throw new InvalidDataException("Tile edits are incompatible with this authored world.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                {
                    writer.Write(content);
                    writer.Flush();
                    file.Flush(true);
                }
                if (!File.Exists(path)) File.Move(temporary, path);
                else
                {
                    bool currentValid;
                    try { var previousData = Read(path); currentValid = compatible == null || compatible(previousData); }
                    catch (Exception error) when (IsExpectedFailure(error)) { currentValid = false; }
                    // A malformed primary is preserved separately; it must not replace the valid .bak.
                    string previous = currentValid ? path + ".bak" : path + ".invalid-" + Guid.NewGuid().ToString("N");
                    File.Replace(temporary, path, previous);
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static bool IsExpectedFailure(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
            error is XmlException || error is ArgumentException || error is NotSupportedException;
    }
}
