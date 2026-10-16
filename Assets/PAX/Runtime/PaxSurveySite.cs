using UnityEngine;

namespace Pax
{
    public sealed class PaxSurveySite : MonoBehaviour
    {
        [SerializeField] private string siteTitle = "Shelter";
        [SerializeField, TextArea] private string fieldNote = "A shelter survived the storm.";
        [SerializeField, Min(0.1f)] private float visitRadius = 1.25f;
        [SerializeField, Min(0.05f)] private float heightTolerance = 0.4f;
        [SerializeField, Min(0.1f)] private float surveySeconds = 0.4f;
        [SerializeField] private Renderer indicator;
        [SerializeField] private Color awaitingColor = new Color(1, 0.65f, 0.15f);
        [SerializeField] private Color surveyedColor = new Color(0.25f, 1, 0.65f);
        private MaterialPropertyBlock properties;
        private float dwell;
        public string Title => siteTitle;
        public string FieldNote => fieldNote;
        public bool Surveyed { get; private set; }
        public float VisitRadius => visitRadius;

        public void Configure(string title, string note, Renderer marker)
        { siteTitle = title; fieldNote = note; indicator = marker; }
        private void Awake() => ResetSurvey();
        public void ResetSurvey() { Surveyed = false; dwell = 0; Present(); }

        public void Observe(PaxPlayer player, float deltaTime)
        {
            if (Surveyed || !isActiveAndEnabled) return;
            Vector3 delta = player.transform.position - transform.position;
            bool near = player.Grounded && Mathf.Abs(delta.y) <= heightTolerance && new Vector2(delta.x, delta.z).sqrMagnitude <= visitRadius * visitRadius;
            dwell = near ? dwell + deltaTime : 0;
            if (dwell < surveySeconds) return;
            Surveyed = true; Present();
        }
        private void Present()
        {
            if (indicator == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", Surveyed ? surveyedColor : awaitingColor);
            indicator.SetPropertyBlock(properties);
        }
        private void OnDrawGizmosSelected()
        { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, visitRadius); }
    }
}
