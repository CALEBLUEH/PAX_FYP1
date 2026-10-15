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
        private Label status;
        private float nextRefresh;
        public void Configure(UIDocument ui, PaxPlayer motor, PaxBuilder interaction, PaxCutaway visibility)
        { document = ui; player = motor; builder = interaction; cutaway = visibility; }
        private void OnEnable() => status = document.rootVisualElement.Q<Label>("status");
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh || status == null) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            status.text = $"{builder.Mode} | Feet {player.transform.position.y:F1} | Ground reference {player.StableFeetHeight:F1}\n" +
                $"Roof {cutaway.RoofCoverage:P0} | Confinement {cutaway.Confinement:P0} | Occluders {cutaway.ObstructionCount}\n{builder.Status}";
        }
    }
}
