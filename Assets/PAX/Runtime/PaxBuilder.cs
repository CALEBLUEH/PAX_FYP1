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
        [SerializeField, Min(0.5f)] private float actionFeedbackSeconds = 2;
        private readonly RaycastHit[] hits = new RaycastHit[128];
        private Vector2 pointer;
        private bool mine, place;
        private PaxBlock selected;
        private PaxBlock candidate;
        private Vector3Int placement;
        private Renderer mineVisual;
        private Material mineMaterial;
        private string lastAction;
        private float lastActionUntil;
        private bool hasFeedback;
        private Vector3Int? feedbackCell;
        private Vector3Int feedbackPlacement;
        private PaxActionBlock feedbackMiningBlock, feedbackPlacementBlock;
        private int feedbackLayer;
        private float feedbackReach;
        private string selectedMaterialId = "default";
        public string SelectedMaterialId => selectedMaterialId;
        public bool TrySelectMaterial(string id)
        {
            if (!world.TryResolveMaterial(id, out _)) return false;
            selectedMaterialId = id; return true;
        }
        public string Mode => (smartCursor ? "SMART" : "PRECISE") + (targetHiddenSolids ? " + GHOST SOLIDS" : " + VISIBLE");
        public string Status { get; private set; } = "Point at a tile";
        public bool CanPlace { get; private set; }
        public Vector3Int? SelectedCell => selected != null ? selected.Cell : (Vector3Int?)null;
        public Vector3Int Placement => placement;
        public Transform MiningPreview => minePreview;
        public Transform PlacementPreview => placePreview;
        public bool PlacementUsesValidMaterial => placeVisual.sharedMaterial == validMaterial;
        public Vector3Int? CandidateCell => candidate != null ? candidate.Cell : (Vector3Int?)null;
        public PaxActionBlock MiningBlock { get; private set; } = PaxActionBlock.NoTarget;
        public PaxActionBlock PlacementBlock { get; private set; } = PaxActionBlock.NoTarget;
        public string MiningStatus { get; private set; } = "Mine: aim at a tile";
        public string PlacementStatus { get; private set; } = "Place: aim at a tile";
        public string LastAction => Time.unscaledTime <= lastActionUntil ? lastAction : string.Empty;
        public bool MiningUsesInvalidMaterial => mineVisual != null && mineVisual.sharedMaterial == invalidMaterial;

        public void Configure(PaxWorld grid, PaxPlayer motor, Camera camera, Transform mining, Transform placing,
            Renderer placingRenderer, Material valid, Material invalid)
        {
            world = grid; player = motor; view = camera; minePreview = mining; placePreview = placing;
            placeVisual = placingRenderer; validMaterial = valid; invalidMaterial = invalid;
            CacheMiningVisual();
        }
        private void Awake() => CacheMiningVisual();
        private void CacheMiningVisual()
        {
            mineVisual = minePreview.GetComponent<Renderer>();
            mineMaterial = mineVisual.sharedMaterial;
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
            bool changed = false;
            if (mine) changed = candidate != null ? Mine(candidate.Cell) : RejectMissingTarget("mine");
            else if (place) changed = candidate != null ? Place(placement) : RejectMissingTarget("place");
            mine = place = false;
            if (changed)
            {
                Physics.SyncTransforms();
                Select(pointer);
            }
        }

        private bool RejectMissingTarget(string action)
        {
            ShowAction($"Cannot {action}: {Explain(PaxActionBlock.NoTarget)}");
            return false;
        }

        private void ShowAction(string message)
        {
            lastAction = message;
            lastActionUntil = Time.unscaledTime + actionFeedbackSeconds;
        }

        public string Explain(PaxActionBlock reason)
        {
            switch (reason)
            {
                case PaxActionBlock.None: return "Ready";
                case PaxActionBlock.NoTarget: return "Aim at a tile or rotate for a clear view.";
                case PaxActionBlock.OutsideWorld: return "Outside test-island bounds. Choose a cell inside the world.";
                case PaxActionBlock.OutOfReach: return $"Move within {reach:0.#} tiles of the cell (including height).";
                case PaxActionBlock.Occupied: return "Cell already occupied. Choose an empty adjacent cell.";
                case PaxActionBlock.PlayerOverlap: return "Player occupies this cell. Step aside.";
                case PaxActionBlock.Unsupported: return "No face support. Attach to an existing tile.";
                case PaxActionBlock.Occluded: return "A solid tile blocks the path. Approach from a clear side.";
                case PaxActionBlock.Protected: return "Protected foundation. Mine an upper tile.";
                case PaxActionBlock.InvalidMaterial: return "Construction material unavailable. Select a valid palette slot.";
                default: return "Tile was removed. Select another tile.";
            }
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

        public PaxActionBlock EvaluatePlacement(Vector3Int cell)
        {
            if (!world.TryResolveMaterial(selectedMaterialId, out _)) return PaxActionBlock.InvalidMaterial;
            if (!world.InBounds(cell)) return PaxActionBlock.OutsideWorld;
            if (!PaxGrid.InReach(player.Chest, cell, reach)) return PaxActionBlock.OutOfReach;
            if (world.Contains(cell)) return PaxActionBlock.Occupied;
            // Slightly shrink for shared faces; touching a supporting floor is legal.
            var bounds = new Bounds(PaxGrid.Centre(cell), Vector3.one * 0.98f);
            if (bounds.Intersects(player.BodyBounds)) return PaxActionBlock.PlayerOverlap;
            if (!world.HasSupport(cell)) return PaxActionBlock.Unsupported;
            return HasLineOfSight(cell) ? PaxActionBlock.None : PaxActionBlock.Occluded;
        }
        public bool CanPlaceAt(Vector3Int cell) => EvaluatePlacement(cell) == PaxActionBlock.None;
        public PaxActionBlock EvaluateMining(Vector3Int cell)
        {
            if (!world.InBounds(cell)) return PaxActionBlock.OutsideWorld;
            if (!PaxGrid.InReach(player.Chest, cell, reach)) return PaxActionBlock.OutOfReach;
            if (!world.TryGet(cell, out var block)) return PaxActionBlock.MissingTile;
            if (!block.Mineable) return PaxActionBlock.Protected;
            return HasLineOfSight(cell) ? PaxActionBlock.None : PaxActionBlock.Occluded;
        }
        public bool Mine(Vector3Int cell)
        {
            var reason = EvaluateMining(cell);
            if (reason != PaxActionBlock.None) { ShowAction($"Cannot mine {cell}: {Explain(reason)}"); return false; }
            bool changed = world.TryMine(cell);
            ShowAction(changed ? $"Mined {cell}" : $"Cannot mine {cell}: {Explain(PaxActionBlock.MissingTile)}");
            return changed;
        }
        public bool Place(Vector3Int cell)
        {
            var reason = EvaluatePlacement(cell);
            if (reason != PaxActionBlock.None) { ShowAction($"Cannot place {cell}: {Explain(reason)}"); return false; }
            bool changed = world.TryAdd(cell, selectedMaterialId);
            ShowAction(changed ? $"Placed {cell}" : $"Cannot place {cell}: world changed; aim again.");
            return changed;
        }

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
            PaxBlock surface = selected;
            Vector3 surfaceNormal = normal;
            if (smartCursor)
            {
                if (!foundSurface && new Plane(Vector3.up, player.Chest).Raycast(ray, out float distance)) aim = ray.GetPoint(distance);
                Vector3 direction = aim - player.Chest;
                float best = float.PositiveInfinity;
                selected = null;
                var blocks = world.Blocks;
                for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
                {
                    var block = blocks[blockIndex];
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
                else { selected = surface; normal = surfaceNormal; }
            }
            candidate = selected;
            if (selected != null && (!PaxGrid.InReach(player.Chest, selected.Cell, reach) || !HasLineOfSight(selected.Cell))) selected = null;
            minePreview.gameObject.SetActive(candidate != null);
            placePreview.gameObject.SetActive(candidate != null);
            CanPlace = false;
            if (candidate == null)
            {
                MiningBlock = PlacementBlock = PaxActionBlock.NoTarget;
                RefreshFeedback();
                return;
            }
            minePreview.position = PaxGrid.Centre(candidate.Cell);
            placement = candidate.Cell + Vector3Int.RoundToInt(normal);
            placePreview.position = PaxGrid.Centre(placement);
            MiningBlock = EvaluateMining(candidate.Cell);
            PlacementBlock = EvaluatePlacement(placement);
            CanPlace = PlacementBlock == PaxActionBlock.None;
            var placementMaterial = CanPlace ? validMaterial : invalidMaterial;
            var miningMaterial = MiningBlock == PaxActionBlock.None ? mineMaterial : invalidMaterial;
            if (placeVisual.sharedMaterial != placementMaterial) placeVisual.sharedMaterial = placementMaterial;
            if (mineVisual.sharedMaterial != miningMaterial) mineVisual.sharedMaterial = miningMaterial;
            RefreshFeedback();
        }

        private void RefreshFeedback()
        {
            var cell = CandidateCell;
            int layer = cell.HasValue ? cell.Value.y - Mathf.FloorToInt(player.StableFeetHeight + 0.05f) : 0;
            if (hasFeedback && cell == feedbackCell && placement == feedbackPlacement &&
                MiningBlock == feedbackMiningBlock && PlacementBlock == feedbackPlacementBlock && layer == feedbackLayer && reach == feedbackReach) return;
            hasFeedback = true;
            feedbackCell = cell; feedbackPlacement = placement;
            feedbackMiningBlock = MiningBlock; feedbackPlacementBlock = PlacementBlock;
            feedbackLayer = layer; feedbackReach = reach;
            if (!cell.HasValue)
            {
                MiningStatus = "Mine: " + Explain(MiningBlock);
                PlacementStatus = "Place: " + Explain(PlacementBlock);
                Status = MiningStatus + " | " + PlacementStatus;
                return;
            }
            MiningStatus = $"Mine {cell.Value} | Layer {layer:+0;-0;0}: {Explain(MiningBlock)}";
            PlacementStatus = $"Place {placement}: {Explain(PlacementBlock)}";
            Status = MiningStatus + " | " + PlacementStatus;
        }
    }
}
