using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PaxHud : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private PaxBuilder builder;
        [SerializeField] private PaxCutaway cutaway;
        [SerializeField, Min(0.03f)] private float refreshInterval = 0.1f;
        [SerializeField] private Color readyColor = new Color(0.6f, 0.95f, 0.7f);
        [SerializeField] private Color blockedColor = new Color(1, 0.65f, 0.5f);
        private Label status, mining, placement, action;
        private float nextRefresh;
        public void Configure(UIDocument ui, PaxPlayer motor, PaxBuilder interaction, PaxCutaway visibility)
        { document = ui; player = motor; builder = interaction; cutaway = visibility; }
        private void OnEnable()
        {
            var root = document.rootVisualElement;
            status = root.Q<Label>("status");
            mining = root.Q<Label>("mining");
            placement = root.Q<Label>("placement");
            action = root.Q<Label>("action");
        }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh || status == null) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            status.text = $"{builder.Mode} | Feet {player.transform.position.y:F1} | Ground reference {player.StableFeetHeight:F1}\n" +
                $"Roof {cutaway.RoofCoverage:P0} | Confinement {cutaway.Confinement:P0} | Occluders {cutaway.ObstructionCount}";
            if (mining != null) { mining.text = builder.MiningStatus; mining.style.color = builder.MiningBlock == PaxActionBlock.None ? readyColor : blockedColor; }
            if (placement != null) { placement.text = builder.PlacementStatus; placement.style.color = builder.CanPlace ? readyColor : blockedColor; }
            if (action != null) action.text = builder.LastAction;
        }
    }
}
