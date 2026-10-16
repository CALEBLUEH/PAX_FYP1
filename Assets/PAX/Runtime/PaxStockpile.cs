using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    [DefaultExecutionOrder(-150)]
    public sealed class PaxStockpile : MonoBehaviour
    {
        [SerializeField] private PaxWorld world;
        [SerializeField] private PaxBuilder builder;
        [SerializeField] private PaxMaterialPalette palette;
        [SerializeField] private PaxResourceRules rules;
        [SerializeField] private UIDocument document;
        [SerializeField, TextArea] private string instructions = "Finite supplies: mine blocks to gather; each placement costs 1 matching unit. Mine the Stone cache near spawn to repair the beacon.";
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<Vector3Int> harvested = new HashSet<Vector3Int>();
        private bool ready;
        private string error;
        private float nextRefresh;
        public PaxResourceRules Rules => rules;
        public PaxMaterialPalette Palette => palette;
        public bool Ready => ready;
        public int Count(string id) => counts.TryGetValue(id, out int value) ? value : 0;
        private PaxResourceRules.Entry Entry(string id)
        { PaxResourceRules.Entry result = null; for (int i = 0; i < rules.Entries.Count; i++) if (rules.Entries[i] != null && rules.Entries[i].id == id) { if (result != null) return null; result = rules.Entries[i]; } return result; }
        private bool Usable(string id)
        { var e = Entry(id); return e != null && counts.ContainsKey(id) && e.limit >= 1 && e.limit <= PaxResourceRules.MaximumUnits && e.authoredYield >= 0 && e.authoredYield <= e.limit && Count(id) >= 0 && Count(id) <= e.limit; }
        public void Configure(PaxWorld grid, PaxBuilder interaction, PaxMaterialPalette choices, PaxResourceRules configuration, UIDocument ui)
        { world = grid; builder = interaction; palette = choices; rules = configuration; document = ui; }
        private void Awake()
        {
            builder.UseStockpile(this);
            if (!rules.Validate(palette, out error)) return;
            foreach (var entry in rules.Entries) counts.Add(entry.id, entry.start);
        }
        private void Start()
        {
            if (!rules.Validate(palette, out error)) return;
            foreach (var cell in world.AuthoredCells)
                if (world.TryGetAuthored(cell, out var material, out bool mineable) && mineable && !world.TryIdentifyMaterial(material, out _))
                { error = "An authored mineable material has no deliberate resource mapping."; return; }
            ready = true;
        }
        public PaxActionBlock PlacementStatus(string id) => !ready || !Usable(id) ? PaxActionBlock.ResourceUnavailable : Count(id) < 1 ? PaxActionBlock.InsufficientResources : PaxActionBlock.None;
        public PaxActionBlock MiningStatus(Vector3Int cell, out string id, out int amount)
        {
            id = null; amount = 0;
            if (!ready || !world.TryGet(cell, out var block) || !world.TryIdentifyMaterial(block.SurfaceMaterial, out id) || !Usable(id) || block.Solid == null || !block.Solid.gameObject.activeInHierarchy || !block.Solid.enabled || block.Solid.isTrigger)
                return PaxActionBlock.ResourceUnavailable;
            var entry = Entry(id); if (entry == null) return PaxActionBlock.ResourceUnavailable;
            bool original = !harvested.Contains(cell) && world.TryGetAuthored(cell, out var material, out _) && material == block.SurfaceMaterial;
            amount = original ? entry.authoredYield : 1;
            return (long)Count(id) + amount > entry.limit ? PaxActionBlock.StockpileFull : PaxActionBlock.None;
        }
        internal void CommitMine(Vector3Int cell, string id, int amount)
        { counts[id] += amount; if (world.TryGetAuthored(cell, out _, out bool mineable) && mineable) harvested.Add(cell); }
        internal void CommitPlacement(string id) => counts[id]--;
        public PaxResourceSnapshot Capture()
        {
            var data = new PaxResourceSnapshot { Tiles = world.CaptureEdits(), Rules = rules.Signature(palette) };
            foreach (var pair in counts) data.Counts.Add(pair.Key, pair.Value);
            data.Harvested.UnionWith(harvested);
            if (!Validate(data, out string reason)) throw new InvalidDataException(reason);
            return data;
        }
        public bool Validate(PaxResourceSnapshot data, out string reason)
        {
            reason = "Resource snapshot is incompatible or violates material conservation.";
            if (!ready || !rules.Validate(palette, out _) || data == null || data.Rules != rules.Signature(palette) || data.Tiles == null || data.Tiles.Version != 2 || !world.ValidateEdits(data.Tiles, out _)) return false;
            if (data.Counts.Count != rules.Entries.Count || data.Harvested.Count > PaxSaveFile.MaximumEdits) return false;
            var expected = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var entry in rules.Entries)
            {
                if (!data.Counts.TryGetValue(entry.id, out int count) || count < 0 || count > entry.limit) return false;
                expected.Add(entry.id, entry.start);
            }
            foreach (var cell in data.Harvested)
                if (!world.TryGetAuthored(cell, out var material, out bool mineable) || !mineable || !world.TryIdentifyMaterial(material, out string id) || !expected.ContainsKey(id)) return false;
            var removed = new HashSet<Vector3Int>(data.Tiles.Removed);
            var placed = data.Tiles.Placed.ToDictionary(cell => cell, cell => data.Tiles.Materials.TryGetValue(cell, out string id) ? id : "default");
            foreach (var cell in world.AuthoredCells)
            {
                world.TryGetAuthored(cell, out var material, out bool mineable);
                if (!mineable) continue;
                if (!data.Harvested.Contains(cell)) { if (removed.Contains(cell) || placed.ContainsKey(cell)) return false; continue; }
                if (!world.TryIdentifyMaterial(material, out string original) || !expected.ContainsKey(original)) return false;
                expected[original] += Entry(original).authoredYield;
                string present = placed.TryGetValue(cell, out string replacement) ? replacement : !removed.Contains(cell) ? original : null;
                if (present != null) { if (!expected.ContainsKey(present)) return false; expected[present]--; }
            }
            foreach (var pair in placed)
                if (!world.TryGetAuthored(pair.Key, out _, out _)) { if (!expected.ContainsKey(pair.Value)) return false; expected[pair.Value]--; }
            foreach (var pair in expected) if (pair.Value != data.Counts[pair.Key]) return false;
            reason = null; return true;
        }
        internal void RestoreValidated(PaxResourceSnapshot data)
        { counts.Clear(); foreach (var pair in data.Counts) counts.Add(pair.Key, pair.Value); harvested.Clear(); harvested.UnionWith(data.Harvested); nextRefresh = 0; }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            var label = document.rootVisualElement.Q<Label>("resource-stockpile"); if (label == null) return;
            if (!ready) { label.text = error; return; }
            var text = new StringBuilder(instructions).Append('\n');
            foreach (var entry in palette.Entries) text.Append(entry.title).Append(' ').Append(Count(entry.id)).Append('/').Append(Entry(entry.id)?.limit).Append("  ");
            label.text = text.ToString();
        }
    }
}
