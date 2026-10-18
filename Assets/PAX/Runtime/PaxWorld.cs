using System.Collections.Generic;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using UnityEngine;

namespace Pax
{
    public sealed class PaxWorld : MonoBehaviour, IPaxSnapshotWorld
    {
        [SerializeField] private PaxBlock blockPrefab;
        [SerializeField] private Transform blockRoot;
        [SerializeField] private List<PaxBlock> blocks = new List<PaxBlock>();
        [SerializeField] private Vector3Int minimum = new Vector3Int(-12, -4, -11);
        [SerializeField] private Vector3Int maximum = new Vector3Int(12, 10, 11);
        private readonly Dictionary<Vector3Int, PaxBlock> cells = new Dictionary<Vector3Int, PaxBlock>();
        public IReadOnlyList<PaxBlock> Blocks => blocks;
        public int Revision { get; private set; }
        public Vector3Int Minimum => minimum;
        public Vector3Int Maximum => maximum;
        private readonly Dictionary<Vector3Int, TileTemplate> authored = new Dictionary<Vector3Int, TileTemplate>();
        private sealed class TileTemplate
        {
            public Material Material;
            public bool Mineable;
        }
        public string SaveSignature { get; private set; }
        public IEnumerable<Vector3Int> AuthoredCells => authored.Keys;
        public bool TryGetAuthored(Vector3Int cell, out Material material, out bool mineable)
        {
            material = null; mineable = false;
            if (!authored.TryGetValue(cell, out var template)) return false;
            material = template.Material; mineable = template.Mineable; return true;
        }
        public bool TryIdentifyMaterial(Material material, out string id)
        {
            id = null;
            if (material == blockPrefab.SurfaceMaterial && TryResolveMaterial("default", out _)) { id = "default"; return true; }
            return constructionPalette != null && constructionPalette.TryIdentify(material, out id) && TryResolveMaterial(id, out _);
        }
        private PaxMaterialPalette constructionPalette;
        public bool UsePalette(PaxMaterialPalette palette, out string reason)
        {
            reason = null;
            if (palette == null || !palette.Validate(out reason)) return false;
            if (!palette.TryResolve("default", out var material) || material != blockPrefab.SurfaceMaterial)
            { reason = "Default palette material must match the legacy tile prefab material."; return false; }
            constructionPalette = palette;
            return true;
        }
        public bool TryResolveMaterial(string id, out Material material)
        {
            material = null;
            if (!PaxMaterialPalette.IsValidId(id)) return false;
            if (constructionPalette == null) { if (id != "default") return false; material = blockPrefab.SurfaceMaterial; return true; }
            return constructionPalette.TryResolve("default", out var legacy) && legacy == blockPrefab.SurfaceMaterial && constructionPalette.TryResolve(id, out material);
        }
        private string IdentifyMaterial(Material material)
        {
            if (TryIdentifyMaterial(material, out string id)) return id;
            throw new System.IO.InvalidDataException("Placed material is not registered in this world's construction palette. Existing saves were kept.");
        }

        private void Awake()
        {
            Reindex();
            foreach (var block in blocks) authored.Add(block.Cell, new TileTemplate { Material = block.SurfaceMaterial, Mineable = block.Mineable });
            SaveSignature = PaxSnapshotValidation.Signature(this);
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

        public bool TryAdd(Vector3Int cell) => TryAdd(cell, "default");
        public bool TryAdd(Vector3Int cell, string materialId)
        {
            if (!InBounds(cell) || Contains(cell) || !TryResolveMaterial(materialId, out var material)) return false;
            var block = Instantiate(blockPrefab, PaxGrid.Centre(cell), Quaternion.identity, blockRoot);
            block.name = $"Tile {cell.x},{cell.y},{cell.z}";
            block.Configure(cell, block.GetComponent<Renderer>(), block.GetComponent<Collider>());
            block.GetComponent<Renderer>().sharedMaterial = material;
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

        internal bool TryAddBatch(IReadOnlyList<PaxBlueprintPlacement> plan)
        {
            if (plan == null || plan.Count < 2 || plan.Count > PaxConstructionBlueprint.MaximumCells) return false;
            var unique = new HashSet<Vector3Int>(); var materials = new List<Material>();
            foreach (var tile in plan)
            {
                if (!InBounds(tile.Cell) || Contains(tile.Cell) || !unique.Add(tile.Cell) || !TryResolveMaterial(tile.MaterialId,out var material)) return false;
                materials.Add(material);
            }
            // Stage inactive prefab instances before publishing anything to the live grid.
            var staged = new List<PaxBlock>();
            try
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    var block = Instantiate(blockPrefab,PaxGrid.Centre(plan[i].Cell),Quaternion.identity,blockRoot); staged.Add(block);
                    block.gameObject.SetActive(false); block.name = "Tile " + plan[i].Cell;
                    block.Configure(plan[i].Cell,block.GetComponent<Renderer>(),block.GetComponent<Collider>()); block.GetComponent<Renderer>().sharedMaterial = materials[i];
                }
            }
            catch (Exception)
            { foreach (var block in staged) if (block != null) { block.gameObject.SetActive(false); Destroy(block.gameObject); } return false; }
            foreach (var block in staged) { cells.Add(block.Cell,block); blocks.Add(block); block.gameObject.SetActive(true); }
            Revision++; Physics.SyncTransforms(); return true;
        }

        public PaxTileEdits CaptureEdits()
        {
            var data = new PaxTileEdits { World = SaveSignature };
            foreach (var pair in authored)
                if (!cells.TryGetValue(pair.Key, out var block) || block.SurfaceMaterial != pair.Value.Material || block.Mineable != pair.Value.Mineable)
                    data.Removed.Add(pair.Key);
            foreach (var block in blocks)
                if (!authored.TryGetValue(block.Cell, out var original) || block.SurfaceMaterial != original.Material || block.Mineable != original.Mineable)
                { data.Placed.Add(block.Cell); data.Materials.Add(block.Cell, IdentifyMaterial(block.SurfaceMaterial)); }
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

        private bool TryValidateEdits(PaxTileEdits data, out HashSet<Vector3Int> removed, out Dictionary<Vector3Int, Material> placed, out string reason)
            => PaxSnapshotValidation.Tiles(this, data, out removed, out placed, out reason);

        public bool TryApplyEdits(PaxTileEdits data, Bounds playerBounds, out string reason)
        {
            if (!TryValidateEdits(data, out var removed, out var placed, out reason)) return false;
            var target = new Dictionary<Vector3Int, TileTemplate>();
            foreach (var pair in authored) if (!removed.Contains(pair.Key)) target.Add(pair.Key, pair.Value);
            foreach (var pair in placed) target.Add(pair.Key, new TileTemplate { Material = pair.Value, Mineable = true });
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
