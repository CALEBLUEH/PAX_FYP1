using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Pax
{
    // The live world and authored menu catalog use one set of existing snapshot rules.
    public interface IPaxSnapshotWorld
    {
        string SaveSignature { get; }
        Vector3Int Minimum { get; }
        Vector3Int Maximum { get; }
        IEnumerable<Vector3Int> AuthoredCells { get; }
        bool TryGetAuthored(Vector3Int cell, out Material material, out bool mineable);
        bool TryIdentifyMaterial(Material material, out string id);
        bool TryResolveMaterial(string id, out Material material);
        bool InBounds(Vector3Int cell);
    }
    public static class PaxSnapshotValidation
    {
        public static string Signature(IPaxSnapshotWorld world)
        {
            var minimum = world.Minimum; var maximum = world.Maximum; var description = new StringBuilder();
            description.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2}|{3},{4},{5}\n", minimum.x, minimum.y, minimum.z, maximum.x, maximum.y, maximum.z);
            foreach (var cell in world.AuthoredCells.OrderBy(c => c.x).ThenBy(c => c.y).ThenBy(c => c.z))
            { world.TryGetAuthored(cell, out var material, out bool mineable); description.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2}:{3}:{4}\n", cell.x, cell.y, cell.z, material.name, mineable ? '1' : '0'); }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(description.ToString()))).Replace("-", "");
        }
        public static bool Tiles(IPaxSnapshotWorld world, PaxTileEdits data, out HashSet<Vector3Int> removed, out Dictionary<Vector3Int, Material> placed, out string reason)
        {
            reason = null; removed = null; placed = null;
            if (data == null || (data.Version != 1 && data.Version != PaxTileEdits.CurrentVersion) || data.World != world.SaveSignature)
            { reason = "Save belongs to a different authored world or version."; return false; }
            if (data.Removed.Count + data.Placed.Count > PaxSaveFile.MaximumEdits) { reason = "Save exceeds the tile-edit limit."; return false; }
            removed = new HashSet<Vector3Int>();
            foreach (var cell in data.Removed)
                if (!removed.Add(cell) || !world.TryGetAuthored(cell, out _, out bool mineable) || !mineable)
                { reason = "Save has repeated, unknown or protected removals."; return false; }
            var placedCells = new HashSet<Vector3Int>(data.Placed);
            if (data.Version == 1 && data.Materials.Count != 0 || data.Materials.Keys.Any(cell => !placedCells.Contains(cell)))
            { reason = "Save has incompatible or orphaned material metadata."; return false; }
            placed = new Dictionary<Vector3Int, Material>();
            foreach (var cell in data.Placed)
            {
                if (placed.ContainsKey(cell) || !world.InBounds(cell) || world.TryGetAuthored(cell, out _, out _) && !removed.Contains(cell))
                { reason = "Save has repeated, occupied or out-of-bounds placements."; return false; }
                string id = data.Version == 1 || !data.Materials.TryGetValue(cell, out string savedId) ? "default" : savedId;
                if (!world.TryResolveMaterial(id, out var material)) { reason = "Save uses an unknown or invalid construction material ID."; return false; }
                placed.Add(cell, material);
            }
            return true;
        }
        public static bool Resources(IPaxSnapshotWorld world, PaxResourceRules rules, PaxMaterialPalette palette, PaxResourceSnapshot data, out string reason)
        {
            reason = "Resource snapshot is incompatible or violates material conservation.";
            if (!rules.Validate(palette, out _) || data == null || data.Rules != rules.Signature(palette) || data.Tiles == null || data.Tiles.Version != 2 || !Tiles(world, data.Tiles, out _, out _, out _)) return false;
            if (data.Counts.Count != rules.Entries.Count || data.Harvested.Count > PaxSaveFile.MaximumEdits) return false;
            var expected = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var entry in rules.Entries) { if (!data.Counts.TryGetValue(entry.id, out int count) || count < 0 || count > entry.limit) return false; expected.Add(entry.id, entry.start); }
            foreach (var cell in data.Harvested)
                if (!world.TryGetAuthored(cell, out var material, out bool mineable) || !mineable || !world.TryIdentifyMaterial(material, out string id) || !expected.ContainsKey(id)) return false;
            var removed = new HashSet<Vector3Int>(data.Tiles.Removed);
            var placed = data.Tiles.Placed.ToDictionary(cell => cell, cell => data.Tiles.Materials.TryGetValue(cell, out string id) ? id : "default");
            foreach (var cell in world.AuthoredCells)
            {
                world.TryGetAuthored(cell, out var material, out bool mineable); if (!mineable) continue;
                if (!data.Harvested.Contains(cell)) { if (removed.Contains(cell) || placed.ContainsKey(cell)) return false; continue; }
                if (!world.TryIdentifyMaterial(material, out string original) || !expected.ContainsKey(original)) return false;
                expected[original] += rules.Entries.First(e => e.id == original).authoredYield;
                string present = placed.TryGetValue(cell, out string replacement) ? replacement : !removed.Contains(cell) ? original : null;
                if (present != null) { if (!expected.ContainsKey(present)) return false; expected[present]--; }
            }
            foreach (var pair in placed) if (!world.TryGetAuthored(pair.Key, out _, out _)) { if (!expected.ContainsKey(pair.Value)) return false; expected[pair.Value]--; }
            foreach (var pair in expected) if (pair.Value != data.Counts[pair.Key]) return false;
            reason = null; return true;
        }
    }
}
