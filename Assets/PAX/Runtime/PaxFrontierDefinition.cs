using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pax
{
    [CreateAssetMenu(menuName = "PAX/Finite frontier generation definition")]
    public sealed class PaxFrontierDefinition : ScriptableObject
    {
        public PaxGenerationDefinition terrain;
        [Range(2, 4)] public int crossingLength = 3;
        [Range(1, 2)] public int lookoutHeight = 2;
        public void Validate()
        {
            if (terrain == null) throw new ArgumentException("Assign a saved island terrain definition.");
            terrain.Validate();
            if (!terrain.house || !terrain.cave || crossingLength < 2 || crossingLength > 4 || lookoutHeight < 1 || lookoutHeight > 2)
                throw new ArgumentException("Frontiers require House and Cave, crossing length 2-4 and lookout height 1-2.");
        }
    }

    // Pure, bounded overlay on the existing terraced-island generator. Gameplay never calls it.
    public sealed class PaxFrontierLayout
    {
        public readonly List<PaxGeneratedTile> Tiles = new List<PaxGeneratedTile>();
        public readonly List<PaxGeneratedTile> Supplies = new List<PaxGeneratedTile>();
        public PaxGeneratedStructure[] Structures { get; private set; }
        public Vector3Int[] Crossing { get; private set; }
        public Vector3Int[] Steps { get; private set; }
        public Vector3 Spawn { get; private set; }
        public Vector3 Relay { get; private set; }
        public Vector3 NearBank { get; private set; }
        public Vector3 FarBank { get; private set; }
        public Vector3Int Minimum { get; private set; }
        public Vector3Int Maximum { get; private set; }

        private static uint Hash(int seed, int value)
        {
            unchecked { uint h = (uint)seed ^ (uint)value * 0x9E3779B9u; h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; return h ^ (h >> 16); }
        }
        public static PaxFrontierLayout Generate(PaxFrontierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            var island = PaxIslandLayout.Generate(definition.terrain);
            var result = new PaxFrontierLayout { Structures = island.Structures.ToArray(), Spawn = island.Spawn, Minimum = island.Minimum, Maximum = island.Maximum };
            var cells = island.Tiles.ToDictionary(tile => tile.Cell);
            void Tile(int x, int y, int z, PaxTerrainMaterial material, bool mineable = false)
            { var cell = new Vector3Int(x, y, z); cells[cell] = new PaxGeneratedTile(cell, material, mineable); }
            void Floor(int x, int z)
            {
                for (int y = -2; y <= result.Maximum.y; y++) cells.Remove(new Vector3Int(x, y, z));
                Tile(x, -2, z, PaxTerrainMaterial.Earth); Tile(x, -1, z, PaxTerrainMaterial.Grass);
            }
            int lane = (int)(Hash(definition.terrain.seed, 71) % 3) - 1;
            int farZ = 4 + definition.crossingLength, deckZ = farZ + 1 + definition.lookoutHeight;
            // Three-wide protected route, with a deliberate repair trench and one/two missing steps.
            for (int x = lane - 1; x <= lane + 1; x++) for (int z = -3; z <= deckZ + 1; z++) Floor(x, z);
            for (int x = lane - 1; x <= lane + 1; x++) for (int z = 4; z < farZ; z++)
                for (int y = -2; y <= -1; y++) cells.Remove(new Vector3Int(x, y, z));
            result.Crossing = Enumerable.Range(4, definition.crossingLength).Select(z => new Vector3Int(lane, -1, z)).ToArray();
            result.Steps = Enumerable.Range(0, definition.lookoutHeight).Select(i => new Vector3Int(lane, i, farZ + 1 + i)).ToArray();
            foreach (var step in result.Steps) for (int y = 0; y < step.y; y++) Tile(step.x, y, step.z, PaxTerrainMaterial.Rock);
            for (int x = lane - 1; x <= lane + 1; x++) for (int z = deckZ; z <= deckZ + 1; z++)
                for (int y = 0; y < definition.lookoutHeight; y++) Tile(x, y, z, PaxTerrainMaterial.Rock);
            result.NearBank = new Vector3(lane, 0.03f, 3);
            result.FarBank = new Vector3(lane, 0.03f, farZ);
            result.Relay = new Vector3(lane, definition.lookoutHeight + 0.03f, deckZ);
            void Cache(string kind, int count, PaxTerrainMaterial material, int salt)
            {
                var structure = result.Structures.Single(s => s.kind == kind);
                for (int x = structure.centre.x - 3; x <= structure.centre.x + 3; x++)
                    for (int z = structure.door.z - 4; z <= structure.door.z - 1; z++) Floor(x, z);
                // Centre line and the existing cross-island approach remain clear, including at maximum budget.
                var candidates = new[] { -2, -1, 1, 2 }.OrderBy(x => Hash(definition.terrain.seed, salt + x)).ToArray();
                foreach (int offset in candidates.Take(count))
                {
                    var cell = new Vector3Int(structure.centre.x + offset, 0, structure.door.z - 3);
                    Tile(cell.x, cell.y, cell.z, material, true);
                    result.Supplies.Add(cells[cell]);
                }
            }
            Cache("House", definition.crossingLength, PaxTerrainMaterial.Timber, 103);
            Cache("Cave", definition.lookoutHeight, PaxTerrainMaterial.Rock, 211);
            result.Tiles.AddRange(cells.Values.OrderBy(t => t.Cell.x).ThenBy(t => t.Cell.y).ThenBy(t => t.Cell.z));
            return result;
        }
    }
}
