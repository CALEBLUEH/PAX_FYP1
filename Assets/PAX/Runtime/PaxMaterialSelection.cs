using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    [DefaultExecutionOrder(-200)]
    public sealed class PaxMaterialSelection : MonoBehaviour
    {
        [SerializeField] private PaxPause pause;
        public void UsePause(PaxPause owner) => pause = owner;
        [SerializeField] private PaxWorld world;
        [SerializeField] private PaxBuilder builder;
        [SerializeField] private PaxMaterialPalette palette;
        [SerializeField] private Renderer previewSample;
        [SerializeField] private UIDocument document;
        [SerializeField, Range(1, 9)] private int initialNumber = 1;
        [SerializeField] private string retainedMessage = "Unavailable selection; current material kept.";
        [SerializeField] private Color invalidSampleColor = new Color(1, 0.2f, 0.2f);
        private MaterialPropertyBlock properties;
        private string message;
        private float nextRefresh;
        public PaxMaterialPalette Palette => palette;
        public Renderer PreviewSample => previewSample;
        public void Configure(PaxWorld grid, PaxBuilder interaction, PaxMaterialPalette choices, Renderer sample, UIDocument ui)
        { world = grid; builder = interaction; palette = choices; previewSample = sample; document = ui; }
        private void Awake()
        {
            if (!world.UsePalette(palette, out message)) return;
            SelectNumber(initialNumber);
        }
        public bool SelectNumber(int number)
        {
            if (pause != null && pause.BlocksActions) return false;
            if (!palette.Validate(out string reason) || number < 1 || number > palette.Entries.Count || !builder.TrySelectMaterial(palette.Entries[number - 1].id))
            { message = reason ?? retainedMessage; return false; }
            previewSample.sharedMaterial = palette.Entries[number - 1].material;
            message = string.Empty; nextRefresh = 0; return true;
        }
        private void Update()
        {
            for (int i = 0; i < 9 && (pause == null || !pause.BlocksActions); i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
                { SelectNumber(i + 1); break; }
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetFloat("_Visibility", builder.CanPlace ? 0.45f : 0.1f);
            properties.SetFloat("_GhostRelevance", 0);
            properties.SetFloat("_WorldSunlight", 1);
            properties.SetColor("_GhostColor", builder.CanPlace ? Color.clear : invalidSampleColor);
            previewSample.SetPropertyBlock(properties);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            var label = document.rootVisualElement.Q<Label>("construction-palette");
            if (label == null) return;
            var text = new StringBuilder(palette.Title).Append('\n');
            if (palette.Validate(out _))
                for (int i = 0; i < palette.Entries.Count; i++)
                {
                    var entry = palette.Entries[i];
                    if (i > 0) text.Append("  ·  ");
                    text.Append(i + 1).Append(' ').Append(entry.title);
                    if (entry.id == builder.SelectedMaterialId) text.Append(" [selected]");
                }
            text.Append("\nMaterial ID: ").Append(builder.SelectedMaterialId).Append(" / Green outline = valid; red = blocked.");
            if (!string.IsNullOrEmpty(message)) text.Append('\n').Append(message);
            label.text = text.ToString();
        }
    }
}
