using System.Collections.Generic;
using UnityEngine;

namespace Pax
{
    [DefaultExecutionOrder(100)]
    public sealed class PaxCutaway : MonoBehaviour
    {
        [SerializeField] private PaxWorld world;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private Camera view;
        [SerializeField, Min(0.02f)] private float sampleInterval = 0.1f;
        [SerializeField, Min(1)] private float fadeSharpness = 10;
        [SerializeField, Min(2)] private float localRadius = 8;
        [SerializeField, Min(0.1f)] private float boundaryBlend = 3;
        [SerializeField, Min(1)] private float ghostRadius = 7;
        [SerializeField, Min(1)] private float ghostVerticalRange = 5;
        [SerializeField, Range(1, 6)] private int roofRadius = 3;
        [SerializeField, Range(4, 16)] private int confinementRadius = 8;
        [SerializeField, Range(32, 512)] private int floodBudget = 192;
        [SerializeField, Range(0, 0.15f)] private float obstructionVisibility = 0.04f;
        [SerializeField, Range(0.1f, 1)] private float obstructionRadius = 0.6f;
        [SerializeField] private Color aboveGhost = new Color(0.2f, 0.9f, 1);
        [SerializeField] private Color belowGhost = new Color(1, 0.45f, 0.1f);
        [SerializeField] private Color levelGhost = new Color(0.75f, 1, 0.25f);
        private readonly Dictionary<PaxBlock, float> targets = new Dictionary<PaxBlock, float>();
        private readonly HashSet<PaxBlock> obstructions = new HashSet<PaxBlock>();
        private readonly HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        private readonly Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        private readonly RaycastHit[] rayHits = new RaycastHit[128];
        private static readonly Vector3Int[] Directions = { Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back };
        private float nextSample;
        private int revision = -1;
        public float Confinement { get; private set; }
        public float RoofCoverage { get; private set; }
        public float RoofGap { get; private set; }
        public int FloodVisited => visited.Count;
        public int ObstructionCount => obstructions.Count;
        public bool PresentationEnabled { get; set; } = true;

        public void Configure(PaxWorld grid, PaxPlayer motor, Camera camera)
        {
            world = grid; player = motor; view = camera;
        }

        private void LateUpdate()
        {
            if (!PresentationEnabled) return;
            if (Time.unscaledTime >= nextSample || revision != world.Revision)
            {
                RefreshNow();
                nextSample = Time.unscaledTime + sampleInterval;
            }
            float blend = 1 - Mathf.Exp(-fadeSharpness * Time.deltaTime);
            int feet = Mathf.FloorToInt(player.StableFeetHeight + 0.05f);
            foreach (var pair in targets)
            {
                var block = pair.Key;
                if (block == null) continue;
                float value = Mathf.Lerp(block.Visibility, pair.Value, blend);
                if (Mathf.Abs(value - pair.Value) < 0.001f) value = pair.Value;
                float relevance = GhostRelevance(block);
                if (Mathf.Abs(value - block.Visibility) < 0.0001f && Mathf.Abs(relevance - block.GhostRelevance) < 0.001f) continue;
                int layer = block.Cell.y - feet;
                Color ghost = layer > 0 ? aboveGhost : layer < 0 ? belowGhost : levelGhost;
                block.Present(value, ghost, relevance);
            }
        }

        public float TargetFor(PaxBlock block) => targets.TryGetValue(block, out float value) ? value : 1;
        public bool IsObstruction(PaxBlock block) => obstructions.Contains(block);
        public float GhostRelevance(PaxBlock block)
        {
            Vector3 delta = PaxGrid.Centre(block.Cell) - player.Chest;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            float vertical = Mathf.Abs(delta.y);
            return (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ghostRadius * 0.6f, ghostRadius, horizontal))) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ghostVerticalRange * 0.6f, ghostVerticalRange, vertical)));
        }

        public void RefreshNow()
        {
            revision = world.Revision;
            int feet = Mathf.FloorToInt(player.StableFeetHeight + 0.05f);
            Vector3Int origin = PaxGrid.Cell(new Vector3(player.transform.position.x, feet, player.transform.position.z));
            SampleRoom(origin);
            SampleRoof(origin);
            obstructions.Clear();
            // Orthographic rays start at each target's screen projection, not the camera pivot.
            for (int sample = 0; sample < 3; sample++)
            {
                Vector3 point = player.transform.position + Vector3.up * (0.25f + sample * 0.75f);
                Ray ray = view.ScreenPointToRay(view.WorldToScreenPoint(point));
                float distance = Vector3.Dot(point - ray.origin, ray.direction);
                int count = Physics.SphereCastNonAlloc(ray, obstructionRadius, rayHits, Mathf.Max(0, distance - 0.1f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var block = rayHits[i].collider.GetComponent<PaxBlock>();
                    if (block != null && block.Cell.y + 1 > player.transform.position.y + 0.05f) obstructions.Add(block);
                }
            }
            targets.Clear();
            foreach (var block in world.Blocks)
            {
                if (block == null) continue;
                int layer = block.Cell.y - feet;
                float alpha = PaxGrid.HeightOpacity(layer);
                Vector3 delta = PaxGrid.Centre(block.Cell) - player.Chest;
                float horizontal = new Vector2(delta.x, delta.z).magnitude;
                float relevance = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(localRadius - boundaryBlend, localRadius, horizontal));
                alpha = Mathf.Lerp(1, alpha, relevance);
                if (layer >= 2 && horizontal <= roofRadius + 1)
                {
                    float lowRoof = 1 - Mathf.InverseLerp(2, 7, RoofGap);
                    float preserve = Mathf.Lerp(0.3f, 0.75f, lowRoof) * RoofCoverage * Mathf.Lerp(0.4f, 1, Confinement);
                    alpha = Mathf.Max(alpha, preserve);
                }
                if (delta.sqrMagnitude <= 1.75f * 1.75f) alpha = Mathf.Max(alpha, 0.75f);
                // Obstruction wins over ALL contextual preservation, including nearby walls and low roofs.
                if (obstructions.Contains(block)) alpha = Mathf.Min(alpha, obstructionVisibility);
                targets.Add(block, alpha);
            }
        }

        private void SampleRoof(Vector3Int origin)
        {
            float covered = 0, weights = 0, gaps = 0;
            for (int x = -roofRadius; x <= roofRadius; x++)
            for (int z = -roofRadius; z <= roofRadius; z++)
            {
                int ring = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                float weight = ring <= 1 ? 1 : ring == 2 ? 0.75f : 0.5f;
                weights += weight;
                for (int y = 2; y <= 10; y++)
                {
                    if (!world.Contains(origin + new Vector3Int(x, y, z))) continue;
                    covered += weight; gaps += weight * y;
                    break;
                }
            }
            RoofCoverage = weights > 0 ? covered / weights : 0;
            RoofGap = covered > 0 ? gaps / covered : 10;
        }

        private void SampleRoom(Vector3Int origin)
        {
            visited.Clear(); frontier.Clear();
            visited.Add(origin); frontier.Enqueue(origin);
            bool escaped = false;
            while (frontier.Count > 0 && visited.Count < floodBudget)
            {
                var cell = frontier.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (Mathf.Abs(next.x - origin.x) > confinementRadius || Mathf.Abs(next.z - origin.z) > confinementRadius)
                    { escaped = true; continue; }
                    if (visited.Contains(next) || world.Contains(next) || world.Contains(next + Vector3Int.up)) continue;
                    if (visited.Count >= floodBudget) break;
                    visited.Add(next); frontier.Enqueue(next);
                }
            }
            float directional = 0;
            foreach (var direction in Directions)
            {
                for (int step = 1; step <= confinementRadius; step++)
                {
                    if (!world.Contains(origin + direction * step) && !world.Contains(origin + direction * step + Vector3Int.up)) continue;
                    directional += 1 - (step - 1f) / confinementRadius;
                    break;
                }
            }
            float bounded = escaped || visited.Count >= floodBudget ? 0 : 1 - visited.Count / (float)floodBudget;
            Confinement = Mathf.Clamp01(directional * 0.175f + bounded * 0.3f);
        }

        private void OnDisable()
        {
            if (world == null) return;
            foreach (var block in world.Blocks) if (block != null) block.RestorePresentation();
            targets.Clear();
        }
    }
}
