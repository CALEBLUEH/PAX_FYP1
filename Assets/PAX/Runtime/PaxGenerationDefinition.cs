using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pax
{
    [CreateAssetMenu(menuName = "PAX/Island generation definition")]
    public sealed class PaxGenerationDefinition : ScriptableObject
    {
        public int seed = 16427;
        [Range(12, 16)] public int radius = 12;
        [Range(0, 2)] public int terraceHeight = 2;
        [Range(2, 5)] public int terrainPatchSize = 3;
        public bool house = true;
        public bool cave = true;
        public bool ruin = true;

        public void Validate()
        {
            if (radius < 12 || radius > 16 || terraceHeight < 0 || terraceHeight > 2 || terrainPatchSize < 2 || terrainPatchSize > 5)
                throw new ArgumentException("PAX islands require radius 12-16, terrace height 0-2 and patch size 2-5.");
        }
    }

    public enum PaxTerrainMaterial { Grass, Earth, Timber, Roof, Rock }

    public readonly struct PaxGeneratedTile
    {
        public readonly Vector3Int Cell;
        public readonly PaxTerrainMaterial Material;
        public readonly bool Mineable;
        public PaxGeneratedTile(Vector3Int cell, PaxTerrainMaterial material, bool mineable = true)
        { Cell = cell; Material = material; Mineable = mineable; }
    }

    [Serializable]
    public struct PaxGeneratedStructure
    {
        public string kind;
        public Vector3Int centre;
        public Vector3Int door;
        public int roofHeight;
    }

    // Pure layout calculation. Never called automatically by gameplay, Awake or OnValidate.
    public sealed class PaxIslandLayout
    {
        public readonly List<PaxGeneratedTile> Tiles = new List<PaxGeneratedTile>();
        public readonly List<PaxGeneratedStructure> Structures = new List<PaxGeneratedStructure>();
        public Vector3 Spawn => new Vector3(0, 0.03f, -3);
        public Vector3Int Minimum { get; private set; }
        public Vector3Int Maximum { get; private set; }

        // Fixed integer hashing avoids global Unity RNG state and platform-specific noise.
        private static uint Hash(int seed, int x, int z)
        {
            unchecked
            {
                uint value = (uint)seed ^ ((uint)x * 0x9E3779B9u) ^ ((uint)z * 0x85EBCA6Bu);
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu;
                return value ^ (value >> 16);
            }
        }

        public static PaxIslandLayout Generate(PaxGenerationDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            var layout = new PaxIslandLayout
            {
                Minimum = new Vector3Int(-definition.radius, -3, -definition.radius),
                Maximum = new Vector3Int(definition.radius, 10, definition.radius)
            };
            int jitter = (int)(Hash(definition.seed, 93, 17) % 3) - 1;
            if (definition.house) layout.AddStructure("House", new Vector3Int(-7, 0, 6 + jitter), 3);
            if (definition.cave) layout.AddStructure("Cave", new Vector3Int(7, 0, 6 - jitter), 4);
            if (definition.ruin) layout.AddStructure("Ruin", new Vector3Int(-7 + jitter, 0, -7), 3);
            var cells = new Dictionary<Vector3Int, PaxGeneratedTile>();
            void Tile(int x, int y, int z, PaxTerrainMaterial material, bool mineable = true)
            {
                var cell = new Vector3Int(x, y, z);
                cells[cell] = new PaxGeneratedTile(cell, material, mineable);
            }
            int radius = definition.radius;
            for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                bool flat = Mathf.Abs(x) <= 2 || Mathf.Abs(z + 3) <= 1;
                foreach (var structure in layout.Structures)
                {
                    // Pads and three-wide approaches connect every entrance to the central path.
                    flat |= Mathf.Abs(x - structure.centre.x) <= 3 && Mathf.Abs(z - structure.centre.z) <= 3;
                    flat |= Mathf.Abs(z - (structure.door.z - 2)) <= 1 && x >= Mathf.Min(0, structure.door.x) - 1 && x <= Mathf.Max(0, structure.door.x) + 1;
                }
                int cornerCut = (int)(Hash(definition.seed, x, z) % 2);
                if (!flat && Mathf.Abs(x) + Mathf.Abs(z) > radius * 2 - 5 - cornerCut) continue;
                int patchX = Mathf.FloorToInt((float)x / definition.terrainPatchSize);
                int patchZ = Mathf.FloorToInt((float)z / definition.terrainPatchSize);
                int surface = flat ? -1 : -1 + (int)(Hash(definition.seed, patchX, patchZ) % (uint)(definition.terraceHeight + 1));
                for (int y = -3; y <= surface; y++)
                    Tile(x, y, z, y == surface ? PaxTerrainMaterial.Grass : PaxTerrainMaterial.Earth, y != -3);
            }
            foreach (var structure in layout.Structures)
            for (int x = structure.centre.x - 2; x <= structure.centre.x + 2; x++)
            for (int z = structure.centre.z - 2; z <= structure.centre.z + 2; z++)
            {
                bool boundary = Mathf.Abs(x - structure.centre.x) == 2 || Mathf.Abs(z - structure.centre.z) == 2;
                bool door = x == structure.door.x && z == structure.door.z;
                bool cave = structure.kind == "Cave", ruin = structure.kind == "Ruin";
                int wallHeight = ruin ? 1 + (int)(Hash(definition.seed, x, z) % 3) : structure.roofHeight;
                if (boundary)
                    for (int y = 0; y < wallHeight; y++)
                        if (!door || y >= 2) Tile(x, y, z, cave || ruin ? PaxTerrainMaterial.Rock : PaxTerrainMaterial.Timber);
                if (!ruin || (x == structure.centre.x - 2 && z >= structure.centre.z))
                {
                    Tile(x, structure.roofHeight, z, cave ? PaxTerrainMaterial.Rock : PaxTerrainMaterial.Roof);
                    if (cave) Tile(x, structure.roofHeight + 1, z, PaxTerrainMaterial.Grass);
                }
            }
            layout.Tiles.AddRange(cells.Values);
            layout.Tiles.Sort((a, b) => { int c = a.Cell.x.CompareTo(b.Cell.x); if (c != 0) return c; c = a.Cell.y.CompareTo(b.Cell.y); return c != 0 ? c : a.Cell.z.CompareTo(b.Cell.z); });
            return layout;
        }

        private void AddStructure(string kind, Vector3Int centre, int roofHeight)
        {
            Structures.Add(new PaxGeneratedStructure { kind = kind, centre = centre, door = centre + new Vector3Int(0, 0, -2), roofHeight = roofHeight });
        }
    }
}
