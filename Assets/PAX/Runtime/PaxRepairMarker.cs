using UnityEngine;

namespace Pax
{
    public sealed class PaxRepairMarker : MonoBehaviour
    {
        [SerializeField] private PaxWorld world;
        [SerializeField] private Renderer blueprint;
        public Vector3Int Cell => PaxGrid.Cell(transform.position);
        public bool IsSolid => world != null && world.TryGet(Cell, out var block) && block.Solid != null &&
            block.Solid.enabled && block.Solid.gameObject.activeInHierarchy && !block.Solid.isTrigger && block.Solid.bounds.Contains(PaxGrid.Centre(Cell));
        public void Configure(PaxWorld grid, Renderer visual) { world = grid; blueprint = visual; }
        private void LateUpdate() { if (blueprint != null) blueprint.enabled = !IsSolid; }
        private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireCube(PaxGrid.Centre(Cell), Vector3.one); }
    }
}
