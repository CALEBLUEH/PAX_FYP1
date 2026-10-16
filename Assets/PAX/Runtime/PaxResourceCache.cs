using System.Linq;
using UnityEngine;

namespace Pax
{
    public sealed class PaxResourceCache : MonoBehaviour
    {
        [SerializeField] private Transform[] gatherMarkers;
        public Vector3Int[] Cells => gatherMarkers.Select(marker => PaxGrid.Cell(marker.position)).ToArray();
        public void Configure(Transform[] markers) => gatherMarkers = markers;
        private void OnDrawGizmosSelected()
        { if (gatherMarkers == null) return; Gizmos.color = Color.yellow; foreach (var marker in gatherMarkers) if (marker != null) Gizmos.DrawWireCube(marker.position, Vector3.one); }
    }
}
