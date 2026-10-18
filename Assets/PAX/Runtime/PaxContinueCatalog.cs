using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pax
{
    // Build/authoring metadata, not another player save format. Rows mirror saved scene tiles.
    [Serializable] public sealed class PaxContinueSlot : IPaxSnapshotWorld
    {
        [Serializable] public sealed class Tile { public Vector3Int cell; public Material material; public bool mineable; }
        public string scenePath, folder, file;
        public Vector3Int minimum, maximum;
        public Material defaultMaterial;
        public PaxMaterialPalette palette;
        public PaxResourceRules rules;
        public Tile[] tiles;
        [NonSerialized] private Dictionary<Vector3Int, Tile> index;
        public Vector3Int Minimum => minimum;
        public Vector3Int Maximum => maximum;
        public string SaveSignature => PaxSnapshotValidation.Signature(this);
        public IEnumerable<Vector3Int> AuthoredCells => tiles.Select(tile => tile.cell);
        public void Invalidate() => index = null;
        private void Index() { if (index == null) index = tiles.ToDictionary(tile => tile.cell); }
        public bool TryGetAuthored(Vector3Int cell, out Material material, out bool mineable)
        { Index(); material = null; mineable = false; if (!index.TryGetValue(cell, out var tile)) return false; material = tile.material; mineable = tile.mineable; return true; }
        public bool TryResolveMaterial(string id, out Material material)
        { material = null; return palette != null && palette.TryResolve("default", out var legacy) && legacy == defaultMaterial && palette.TryResolve(id, out material); }
        public bool TryIdentifyMaterial(Material material, out string id)
        { id = null; if (material == defaultMaterial && TryResolveMaterial("default", out _)) { id = "default"; return true; } return palette != null && palette.TryIdentify(material, out id) && TryResolveMaterial(id, out _); }
        public bool InBounds(Vector3Int cell) => cell.x >= minimum.x && cell.x <= maximum.x && cell.y >= minimum.y && cell.y <= maximum.y && cell.z >= minimum.z && cell.z <= maximum.z;
        public bool Validate(PaxResourceSnapshot data)
        {
            if (rules == null || palette == null || tiles == null || tiles.Any(tile => tile == null || tile.material == null) || tiles.Select(tile => tile.cell).Distinct().Count() != tiles.Length) return false;
            return PaxSnapshotValidation.Resources(this, rules, palette, data, out _);
        }
        public string PathFor(string validationDirectory = null) => PaxPlaytestSave.ResolveSlotPath(Application.persistentDataPath, folder, file, validationDirectory);
    }
    [CreateAssetMenu(menuName = "PAX/Continue validation catalog")]
    public sealed class PaxContinueCatalog : ScriptableObject
    {
        [SerializeField] private PaxContinueSlot[] slots;
        public IReadOnlyList<PaxContinueSlot> Slots => slots;
        public void Configure(PaxContinueSlot[] values) => slots = values;
        public PaxContinueSlot ForScene(string path)
        { if (slots == null) return null; var found = slots.Where(slot => slot != null && slot.scenePath == path).ToArray(); return found.Length == 1 ? found[0] : null; }
        private void OnValidate() { if (slots != null) foreach (var slot in slots) slot?.Invalidate(); }
    }
}
