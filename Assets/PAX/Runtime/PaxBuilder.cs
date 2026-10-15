using UnityEngine;

namespace Pax
{
    public sealed class PaxBuilder : MonoBehaviour
    {
        [SerializeField] private PaxWorld world;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private Camera view;
        [SerializeField] private Transform minePreview, placePreview;
        [SerializeField] private Renderer placeVisual;
        [SerializeField] private Material validMaterial, invalidMaterial;
        [SerializeField, Min(1)] private float reach = 5;
        [SerializeField, Range(8, 100)] private float smartConeDegrees = 48;
        [SerializeField] private bool smartCursor = true;
        [SerializeField] private bool targetHiddenSolids;
        private readonly RaycastHit[] hits = new RaycastHit[128];
        private Vector2 pointer;
        private bool mine, place;
        private PaxBlock selected;
        private Vector3Int placement;
        public string Mode => (smartCursor ? "SMART" : "PRECISE") + (targetHiddenSolids ? " + GHOST SOLIDS" : " + VISIBLE");
        public string Status { get; private set; } = "Point at a tile";
        public bool CanPlace { get; private set; }
        public Vector3Int? SelectedCell => selected != null ? selected.Cell : (Vector3Int?)null;
        public Vector3Int Placement => placement;
        public Transform MiningPreview => minePreview;
        public Transform PlacementPreview => placePreview;
        public bool PlacementUsesValidMaterial => placeVisual.sharedMaterial == validMaterial;

        public void Configure(PaxWorld grid, PaxPlayer motor, Camera camera, Transform mining, Transform placing,
            Renderer placingRenderer, Material valid, Material invalid)
        {
            world = grid; player = motor; view = camera; minePreview = mining; placePreview = placing;
            placeVisual = placingRenderer; validMaterial = valid; invalidMaterial = invalid;
        }
        public void ToggleCursor() => smartCursor = !smartCursor;
        public void ToggleGhostTargets() => targetHiddenSolids = !targetHiddenSolids;
        public void SetPointer(Vector2 screenPoint, bool minePressed, bool placePressed)
        {
            pointer = screenPoint; mine |= minePressed; place |= placePressed;
        }

        private void LateUpdate()
        {
            Select(pointer);
            if (mine && selected != null) Mine(selected.Cell);
            else if (place && CanPlace) Place(placement);
            mine = place = false;
        }

        public bool HasLineOfSight(Vector3Int cell)
        {
            Vector3 delta = PaxGrid.Centre(cell) - player.Chest;
            if (Physics.Raycast(player.Chest, delta.normalized, out var hit, delta.magnitude + 0.01f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var block = hit.collider.GetComponent<PaxBlock>();
                return block != null && block.Cell == cell;
            }
            return true;
        }

        public bool CanPlaceAt(Vector3Int cell)
        {
            if (!world.InBounds(cell) || world.Contains(cell) || !world.HasSupport(cell) || !PaxGrid.InReach(player.Chest, cell, reach)) return false;
            // Slightly shrink for shared faces; touching a supporting floor is legal.
            var bounds = new Bounds(PaxGrid.Centre(cell), Vector3.one * 0.98f);
            if (bounds.Intersects(player.BodyBounds)) return false;
            return HasLineOfSight(cell);
        }
        public bool Mine(Vector3Int cell) => PaxGrid.InReach(player.Chest, cell, reach) && HasLineOfSight(cell) && world.TryMine(cell);
        public bool Place(Vector3Int cell) => CanPlaceAt(cell) && world.TryAdd(cell);

        public void Select(Vector2 screenPoint)
        {
            Ray ray = view.ScreenPointToRay(screenPoint);
            int count = Physics.RaycastNonAlloc(ray, hits, 100, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            selected = null;
            float nearest = float.PositiveInfinity;
            Vector3 normal = Vector3.up;
            Vector3 aim = player.Chest;
            bool foundSurface = false;
            for (int i = 0; i < count; i++)
            {
                var block = hits[i].collider.GetComponent<PaxBlock>();
                if (block == null || (!targetHiddenSolids && block.Visibility < 0.12f) || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance; aim = hits[i].point; normal = hits[i].normal;
                selected = block; foundSurface = true;
            }
            if (smartCursor)
            {
                if (!foundSurface && new Plane(Vector3.up, player.Chest).Raycast(ray, out float distance)) aim = ray.GetPoint(distance);
                Vector3 direction = aim - player.Chest;
                float best = float.PositiveInfinity;
                selected = null;
                foreach (var block in world.Blocks)
                {
                    if (block == null || !block.Mineable || (!targetHiddenSolids && block.Visibility < 0.12f) || !PaxGrid.InReach(player.Chest, block.Cell, reach)) continue;
                    Vector3 delta = PaxGrid.Centre(block.Cell) - player.Chest;
                    float angle = Vector3.Angle(direction, delta);
                    float score = delta.magnitude + angle * 0.035f;
                    if (angle > smartConeDegrees || score >= best || !HasLineOfSight(block.Cell)) continue;
                    selected = block; best = score;
                }
                if (selected != null)
                {
                    Vector3 toward = player.Chest - PaxGrid.Centre(selected.Cell);
                    Vector3 abs = new Vector3(Mathf.Abs(toward.x), Mathf.Abs(toward.y), Mathf.Abs(toward.z));
                    normal = abs.y >= abs.x && abs.y >= abs.z ? Vector3.up * Mathf.Sign(toward.y) :
                        abs.x >= abs.z ? Vector3.right * Mathf.Sign(toward.x) : Vector3.forward * Mathf.Sign(toward.z);
                }
            }
            if (selected != null && (!PaxGrid.InReach(player.Chest, selected.Cell, reach) || !HasLineOfSight(selected.Cell))) selected = null;
            minePreview.gameObject.SetActive(selected != null);
            placePreview.gameObject.SetActive(selected != null);
            CanPlace = false;
            if (selected == null) { Status = "No reachable visible tile"; return; }
            minePreview.position = PaxGrid.Centre(selected.Cell);
            placement = selected.Cell + Vector3Int.RoundToInt(normal);
            placePreview.position = PaxGrid.Centre(placement);
            CanPlace = CanPlaceAt(placement);
            placeVisual.sharedMaterial = CanPlace ? validMaterial : invalidMaterial;
            int level = selected.Cell.y - Mathf.FloorToInt(player.StableFeetHeight + 0.05f);
            Status = $"Mine {selected.Cell} | Layer {level:+0;-0;0} | Place {placement} {(CanPlace ? "ready" : "blocked")}";
        }
    }
}
