using System.Collections.Generic;
using UnityEngine;

namespace Pax
{
    // Data only: scene references, camera visibility and lighting never enter the save format.
    public sealed class PaxTileEdits
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public string World;
        public readonly List<Vector3Int> Removed = new List<Vector3Int>();
        public readonly List<Vector3Int> Placed = new List<Vector3Int>();
    }
}
