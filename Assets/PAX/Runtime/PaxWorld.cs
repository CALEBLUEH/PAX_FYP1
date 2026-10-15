using System.Collections.Generic;
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

        private void Awake() => Reindex();
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
    }
}
