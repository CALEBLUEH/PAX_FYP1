using UnityEngine;

namespace Pax
{
    public static class PaxGrid
    {
        public static readonly Vector3Int[] Neighbours =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.left,
            Vector3Int.right, Vector3Int.forward, Vector3Int.back
        };

        // X/Z denote tile centres; Y denotes the bottom face of a tile.
        public static Vector3 Centre(Vector3Int cell) => (Vector3)cell + Vector3.up * 0.5f;
        public static Vector3Int Cell(Vector3 position) => Vector3Int.FloorToInt(position + new Vector3(0.5f, 0, 0.5f));
        public static float HeightOpacity(int relativeLayer)
        {
            if (relativeLayer < 0) return 1;
            switch (relativeLayer)
            {
                case 0: return 0.75f;
                case 1: return 0.5f;
                case 2: return 0.25f;
                case 3: return 0.1f;
                default: return 0;
            }
        }

        public static bool InReach(Vector3 origin, Vector3Int cell, float reach) =>
            (Centre(cell) - origin).sqrMagnitude <= reach * reach + 0.0001f;
    }
}
