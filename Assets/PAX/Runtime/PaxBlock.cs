using UnityEngine;

namespace Pax
{
    [DisallowMultipleComponent]
    public sealed class PaxBlock : MonoBehaviour
    {
        [SerializeField] private Vector3Int cell;
        [SerializeField] private Renderer visual;
        [SerializeField] private Collider solid;
        [SerializeField] private Renderer solidShadow;
        [SerializeField] private bool mineable = true;
        private MaterialPropertyBlock properties;
        private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
        private static readonly int GhostId = Shader.PropertyToID("_GhostColor");
        private static readonly int RelevanceId = Shader.PropertyToID("_GhostRelevance");
        private static readonly int SunlightId = Shader.PropertyToID("_WorldSunlight");
        public Vector3Int Cell => cell;
        public bool Mineable => mineable;
        public Collider Solid => solid;
        public Renderer SolidShadow => solidShadow;
        public float Visibility { get; private set; } = 1;
        public float GhostRelevance { get; private set; } = 1;
        public float Sunlight { get; private set; } = 1;

        public void Configure(Vector3Int position, Renderer renderer, Collider collider, bool canMine = true, Renderer shadow = null)
        {
            cell = position;
            visual = renderer;
            solid = collider;
            if (shadow != null) solidShadow = shadow;
            mineable = canMine;
            transform.position = PaxGrid.Centre(position);
        }

        public void Present(float visibility, Color ghostColor, float ghostRelevance = 1)
        {
            Visibility = visibility;
            GhostRelevance = ghostRelevance;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetFloat(VisibilityId, visibility);
            properties.SetColor(GhostId, ghostColor);
            properties.SetFloat(RelevanceId, ghostRelevance);
            properties.SetFloat(SunlightId, Sunlight);
            visual.SetPropertyBlock(properties);
            // Never disable colliders, renderers, or shadow casting to hide a tile.
        }

        public void RestorePresentation() => Present(1, Color.cyan);
        public void SetSunlight(float value)
        {
            Sunlight = value;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetFloat(SunlightId, value);
            visual.SetPropertyBlock(properties);
        }
    }
}
