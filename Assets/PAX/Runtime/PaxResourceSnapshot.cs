using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Pax
{
    public sealed class PaxResourceSnapshot
    {
        public string Rules;
        public PaxTileEdits Tiles;
        public readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly HashSet<Vector3Int> Harvested = new HashSet<Vector3Int>();
        public string Encode()
        {
            return new XElement("pax-resources", new XAttribute("version", "1"), new XAttribute("rules", Rules),
                XElement.Parse(PaxSaveFile.Encode(Tiles)),
                new XElement("stockpile", Counts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new XElement("item", new XAttribute("id", p.Key), new XAttribute("count", p.Value)))),
                new XElement("harvested", Harvested.OrderBy(c => c.x).ThenBy(c => c.y).ThenBy(c => c.z).Select(c => new XElement("cell", new XAttribute("x", c.x), new XAttribute("y", c.y), new XAttribute("z", c.z))))).ToString();
        }
        public static PaxResourceSnapshot Decode(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > PaxSaveFile.MaximumBytes) throw new InvalidDataException("Empty/oversized resource snapshot.");
            using (var input = new StringReader(text))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = PaxSaveFile.MaximumBytes }))
            {
                var root = XDocument.Load(reader).Root;
                if (root == null || root.Name != "pax-resources") throw new InvalidDataException("Finite mode requires a full resource snapshot, not a tile-only save.");
                Attributes(root, "version", "rules");
                if ((string)root.Attribute("version") != "1") throw new InvalidDataException("Unsupported resource snapshot version.");
                string signature = (string)root.Attribute("rules");
                if (signature.Length != 64 || signature.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid resource rules signature.");
                var groups = root.Elements().ToArray();
                if (groups.Length != 3 || groups.Count(g => g.Name == "pax-playtest") != 1 || groups.Count(g => g.Name == "stockpile") != 1 || groups.Count(g => g.Name == "harvested") != 1 || DataText(root)) throw new InvalidDataException("Missing/repeated/unknown snapshot sections.");
                var data = new PaxResourceSnapshot { Rules = signature, Tiles = PaxSaveFile.Decode(root.Element("pax-playtest").ToString()) };
                if (data.Tiles.Version != 2) throw new InvalidDataException("Finite snapshots require typed version 2 tiles.");
                foreach (string name in new[] { "stockpile", "harvested" })
                {
                    var group = root.Element(name);
                    if (group.HasAttributes || DataText(group)) throw new InvalidDataException("Invalid snapshot section.");
                    foreach (var item in group.Elements())
                    {
                        if (item.HasElements || DataText(item)) throw new InvalidDataException("Invalid snapshot item.");
                        if (name == "stockpile")
                        {
                            if (item.Name != "item") throw new InvalidDataException("Unknown stockpile data.");
                            Attributes(item, "id", "count"); string id = (string)item.Attribute("id"); int count = Integer(item, "count");
                            if (!PaxMaterialPalette.IsValidId(id) || count < 0 || count > PaxResourceRules.MaximumUnits || data.Counts.Count >= 9 || !data.Counts.TryAdd(id, count)) throw new InvalidDataException("Invalid/duplicate stockpile ID or count.");
                        }
                        else
                        {
                            if (item.Name != "cell") throw new InvalidDataException("Unknown harvest data.");
                            Attributes(item, "x", "y", "z");
                            if (!data.Harvested.Add(new Vector3Int(Integer(item, "x"), Integer(item, "y"), Integer(item, "z"))) || data.Harvested.Count > PaxSaveFile.MaximumEdits) throw new InvalidDataException("Duplicate/excessive harvest history.");
                        }
                    }
                }
                return data;
            }
        }
        private static bool DataText(XElement e) => e.Nodes().Any(n => n is XText t && !string.IsNullOrWhiteSpace(t.Value));
        private static int Integer(XElement e, string name)
        {
            if (!int.TryParse((string)e.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) throw new InvalidDataException("Resource values must be bounded whole numbers.");
            return value;
        }
        private static void Attributes(XElement e, params string[] names)
        { if (e.Attributes().Count() != names.Length || e.Attributes().Any(a => !names.Any(n => a.Name == n))) throw new InvalidDataException("Missing/unknown snapshot attributes."); }
    }
}
