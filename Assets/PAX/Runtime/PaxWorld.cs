using System.Collections.Generic;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using UnityEngine;

namespace Pax
{
    public sealed class PaxWorld : MonoBehaviour
    {
        [SerializeField] private PaxBlock blockPrefab;
        [SerializeField] private Transform blockRoot;
        [SerializeField] private List<PaxBlock> blocks = new List<PaxBlock>();
        [SerializeField] private Vector3Int minimum = new Vector3Int(-12, -4, -11);
        [SerializeField] private Vector3Int maximum = new Vector3Int(12, 10, 11);
        private readonly Dictionary<Vector3Int, PaxBlock> cells = new Dictionary<Vector3Int, PaxBlock>();
        public IReadOnlyList<PaxBlock> Blocks => blocks;
        public int Revision { get; private set; }
        private readonly Dictionary<Vector3Int, TileTemplate> authored = new Dictionary<Vector3Int, TileTemplate>();
        private sealed class TileTemplate
        {
            public Material Material;
            public bool Mineable;
        }
        public string SaveSignature { get; private set; }

        private void Awake()
        {
            Reindex();
            foreach (var block in blocks) authored.Add(block.Cell, new TileTemplate { Material = block.SurfaceMaterial, Mineable = block.Mineable });
            var description = new StringBuilder();
            description.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2}|{3},{4},{5}\n", minimum.x, minimum.y, minimum.z, maximum.x, maximum.y, maximum.z);
            foreach (var pair in authored.OrderBy(p => p.Key.x).ThenBy(p => p.Key.y).ThenBy(p => p.Key.z))
                description.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2}:{3}:{4}\n", pair.Key.x, pair.Key.y, pair.Key.z,
                    pair.Value.Material.name, pair.Value.Mineable ? '1' : '0');
            using (var hash = SHA256.Create()) SaveSignature = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(description.ToString()))).Replace("-", "");
        }
        public void Configure(PaxBlock prefab, Transform root, List<PaxBlock> authoredBlocks)
        {
            blockPrefab = prefab;
            blockRoot = root;
            blocks = authoredBlocks;
            Reindex();
        }

        public void Reindex()
        {
            cells.Clear();
            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (!cells.TryAdd(block.Cell, block)) Debug.LogError($"Duplicate PAX tile at {block.Cell}", block);
            }
            Revision++;
        }

        public bool Contains(Vector3Int cell) => cells.ContainsKey(cell);
        public bool TryGet(Vector3Int cell, out PaxBlock block) => cells.TryGetValue(cell, out block);
        public bool InBounds(Vector3Int cell) => cell.x >= minimum.x && cell.x <= maximum.x &&
            cell.y >= minimum.y && cell.y <= maximum.y && cell.z >= minimum.z && cell.z <= maximum.z;
        public bool HasSupport(Vector3Int cell)
        {
            foreach (var offset in PaxGrid.Neighbours) if (Contains(cell + offset)) return true;
            return false;
        }

        public bool TryAdd(Vector3Int cell)
        {
            if (!InBounds(cell) || Contains(cell)) return false;
            var block = Instantiate(blockPrefab, PaxGrid.Centre(cell), Quaternion.identity, blockRoot);
            block.name = $"Tile {cell.x},{cell.y},{cell.z}";
            block.Configure(cell, block.GetComponent<Renderer>(), block.GetComponent<Collider>());
            cells.Add(cell, block);
            blocks.Add(block);
            Revision++;
            return true;
        }

        public bool TryMine(Vector3Int cell)
        {
            if (!TryGet(cell, out var block) || !block.Mineable) return false;
            cells.Remove(cell);
            blocks.Remove(block);
            block.gameObject.SetActive(false); // Physics must stop blocking immediately, before end-of-frame destruction.
            Destroy(block.gameObject);
            Revision++;
            return true;
        }

        public PaxTileEdits CaptureEdits()
        {
            var data = new PaxTileEdits { World = SaveSignature };
            foreach (var pair in authored)
                if (!cells.TryGetValue(pair.Key, out var block) || block.SurfaceMaterial != pair.Value.Material || block.Mineable != pair.Value.Mineable)
                    data.Removed.Add(pair.Key);
            foreach (var block in blocks)
                if (!authored.TryGetValue(block.Cell, out var original) || block.SurfaceMaterial != original.Material || block.Mineable != original.Mineable)
                    data.Placed.Add(block.Cell);
            data.Removed.Sort(CompareCells);
            data.Placed.Sort(CompareCells);
            return data;
        }

        private static int CompareCells(Vector3Int a, Vector3Int b)
        {
            int x = a.x.CompareTo(b.x); if (x != 0) return x;
            int y = a.y.CompareTo(b.y); return y != 0 ? y : a.z.CompareTo(b.z);
        }

        public bool ValidateEdits(PaxTileEdits data, out string reason) => TryValidateEdits(data, out _, out _, out reason);

        private bool TryValidateEdits(PaxTileEdits data, out HashSet<Vector3Int> removed, out HashSet<Vector3Int> placed, out string reason)
        {
            reason = null;
            removed = placed = null;
            if (data == null || data.Version != PaxTileEdits.CurrentVersion || data.World != SaveSignature)
            { reason = "Save belongs to a different authored world or version."; return false; }
            if (data.Removed.Count + data.Placed.Count > PaxSaveFile.MaximumEdits)
            { reason = "Save exceeds the tile-edit limit."; return false; }
            removed = new HashSet<Vector3Int>();
            foreach (var cell in data.Removed)
            {
                if (!removed.Add(cell) || !authored.TryGetValue(cell, out var template) || !template.Mineable)
                { reason = "Save has repeated, unknown or protected removals."; return false; }
            }
            placed = new HashSet<Vector3Int>();
            foreach (var cell in data.Placed)
            {
                if (!placed.Add(cell) || !InBounds(cell) || authored.ContainsKey(cell) && !removed.Contains(cell))
                { reason = "Save has repeated, occupied or out-of-bounds placements."; return false; }
            }
            return true;
        }

        public bool TryApplyEdits(PaxTileEdits data, Bounds playerBounds, out string reason)
        {
            if (!TryValidateEdits(data, out var removed, out var placed, out reason)) return false;
            var target = new Dictionary<Vector3Int, TileTemplate>();
            foreach (var pair in authored) if (!removed.Contains(pair.Key)) target.Add(pair.Key, pair.Value);
            foreach (var cell in placed) target.Add(cell, new TileTemplate { Material = blockPrefab.SurfaceMaterial, Mineable = true });
            foreach (var pair in target)
            {
                bool unchanged = cells.TryGetValue(pair.Key, out var current) && current.SurfaceMaterial == pair.Value.Material && current.Mineable == pair.Value.Mineable;
                if (!unchanged && new Bounds(PaxGrid.Centre(pair.Key), Vector3.one * 0.98f).Intersects(playerBounds))
                { reason = "Loading would put a tile inside the player. Step aside and load again."; return false; }
            }
            // Validation completes before any live renderer, collider, list or revision is changed.
            bool changed = false;
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                var block = blocks[i];
                if (target.TryGetValue(block.Cell, out var desired) && block.SurfaceMaterial == desired.Material && block.Mineable == desired.Mineable) continue;
                cells.Remove(block.Cell); blocks.RemoveAt(i);
                block.gameObject.SetActive(false); Destroy(block.gameObject); changed = true;
            }
            foreach (var pair in target)
            {
                if (cells.ContainsKey(pair.Key)) continue;
                var block = Instantiate(blockPrefab, PaxGrid.Centre(pair.Key), Quaternion.identity, blockRoot);
                block.name = $"Tile {pair.Key.x},{pair.Key.y},{pair.Key.z}";
                block.Configure(pair.Key, block.GetComponent<Renderer>(), block.GetComponent<Collider>(), pair.Value.Mineable);
                block.GetComponent<Renderer>().sharedMaterial = pair.Value.Material;
                cells.Add(pair.Key, block); blocks.Add(block); changed = true;
            }
            if (changed) { Revision++; Physics.SyncTransforms(); }
            return true;
        }
    }
}
