using UnityEngine;

namespace Pax
{
    // Additional landmark visuals read the existing mission state; no light/power gameplay state.
    public sealed class PaxCompletionLamps : MonoBehaviour
    {
        [SerializeField] private PaxExpedition expedition;
        [SerializeField] private Renderer[] additionalLamps;
        [SerializeField] private Color dormant = new Color(1, 0.55f, 0.1f), active = new Color(0.2f, 1, 0.65f);
        private MaterialPropertyBlock properties;
        private bool presented, complete;
        public Renderer[] AdditionalLamps => additionalLamps;
        public bool Complete => expedition.Stage == PaxExpeditionStage.Complete;
        public void Configure(PaxExpedition mission, Renderer[] lamps) { expedition = mission; additionalLamps = lamps; }
        private void LateUpdate()
        {
            bool state = Complete; if (presented && complete == state) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            complete = state; presented = true; var color = state ? active : dormant;
            properties.SetColor("_Color", color); properties.SetColor("_EmissionColor", color * 0.5f);
            foreach (var lamp in additionalLamps) lamp.SetPropertyBlock(properties);
        }
    }
}
