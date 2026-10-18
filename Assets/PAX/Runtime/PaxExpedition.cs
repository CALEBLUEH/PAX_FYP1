using UnityEngine;

namespace Pax
{
    public enum PaxExpeditionStage { Survey, Repair, Return, Complete }

    public sealed class PaxExpedition : MonoBehaviour
    {
        [SerializeField] private PaxPause pause;
        public void UsePause(PaxPause owner) => pause = owner;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private PaxSurveySite[] sites;
        [SerializeField] private PaxRepairMarker[] repairs;
        [SerializeField] private Transform beaconReturn;
        [SerializeField] private Renderer beaconIndicator;
        [SerializeField, Min(0.1f)] private float activationRadius = 1.25f;
        [SerializeField, Min(0.05f)] private float activationHeightTolerance = 0.4f;
        [SerializeField] private Color dormantColor = new Color(1, 0.55f, 0.1f);
        [SerializeField] private Color activeColor = new Color(0.2f, 1, 0.65f);
        [SerializeField] private string expeditionTitle = "Quiet Shoal / Survey beacon";
        [SerializeField, TextArea] private string introduction = "Three shelters survived the storm. Read their survey marks and restore the island signal.";
        [SerializeField, TextArea] private string surveyObjective = "Survey each shelter: stand on its amber mark. Any order.";
        [SerializeField, TextArea] private string repairObjective = "Rebuild the six cyan platform cells near spawn using RMB placement. Mine misplaced tiles with LMB.";
        [SerializeField, TextArea] private string returnObjective = "Jump onto the repaired platform. Stand beside the beacon and press F to send the signal.";
        [SerializeField, TextArea] private string completeObjective = "Signal restored. The island remains yours to explore and build. F6 starts a fresh survey; construction stays.";
        [SerializeField, TextArea] private string initialMessage = "Survey visits are session-only. F5/F9/F8 save and load tiles only.";
        [SerializeField, TextArea] private string interruptedMessage = "Signal interrupted: repair the platform and activate the beacon again.";
        [SerializeField, TextArea] private string missingSurveyMessage = "Survey all shelter marks first.";
        [SerializeField, TextArea] private string missingRepairMessage = "The platform is incomplete. Fill every cyan cell with a solid tile.";
        [SerializeField, TextArea] private string alreadyActiveMessage = "Signal already active. Keep exploring or press F6 to replay.";
        [SerializeField, TextArea] private string returnMessage = "Stand on the repaired platform beside the beacon, then press F.";
        [SerializeField, TextArea] private string successMessage = "Signal sent. Survey complete; sandbox controls remain available.";
        [SerializeField, TextArea] private string restartMessage = "New survey started. Existing construction and tile saves are kept.";
        private bool activated, presentedActive;
        private MaterialPropertyBlock properties;
        public string Title => expeditionTitle;
        public string Introduction => introduction;
        public string ActionStatus { get; private set; }
        public PaxSurveySite[] Sites => sites;
        public PaxRepairMarker[] Repairs => repairs;
        public Transform BeaconReturn => beaconReturn;
        public int SurveyedCount { get { int count = 0; foreach (var site in sites) if (site.Surveyed) count++; return count; } }
        public int RepairCount { get { int count = 0; foreach (var repair in repairs) if (repair.IsSolid) count++; return count; } }
        public PaxExpeditionStage Stage => SurveyedCount < sites.Length ? PaxExpeditionStage.Survey : RepairCount < repairs.Length ? PaxExpeditionStage.Repair : activated ? PaxExpeditionStage.Complete : PaxExpeditionStage.Return;
        public string Objective => Stage == PaxExpeditionStage.Survey ? surveyObjective : Stage == PaxExpeditionStage.Repair ? repairObjective : Stage == PaxExpeditionStage.Return ? returnObjective : completeObjective;
        public bool AtBeacon
        {
            get
            {
                Vector3 delta = player.transform.position - beaconReturn.position;
                return player.Grounded && Mathf.Abs(delta.y) <= activationHeightTolerance && new Vector2(delta.x, delta.z).sqrMagnitude <= activationRadius * activationRadius;
            }
        }
        public void Configure(PaxPlayer motor, PaxSurveySite[] surveySites, PaxRepairMarker[] repairCells, Transform returnPoint, Renderer indicator)
        { player = motor; sites = surveySites; repairs = repairCells; beaconReturn = returnPoint; beaconIndicator = indicator; }

        private void Awake() => ActionStatus = initialMessage;

        private void Update()
        {
            if (pause != null && pause.IsPaused) return;
            foreach (var site in sites) site.Observe(player, Time.deltaTime);
            if (activated && (SurveyedCount != sites.Length || RepairCount != repairs.Length))
            { activated = false; ActionStatus = interruptedMessage; }
            Present(Stage == PaxExpeditionStage.Complete);
        }
        public bool TryActivate()
        {
            if (pause != null && pause.BlocksActions) return false;
            if (SurveyedCount < sites.Length) { ActionStatus = missingSurveyMessage; return false; }
            if (RepairCount < repairs.Length) { activated = false; ActionStatus = missingRepairMessage; return false; }
            if (activated) { ActionStatus = alreadyActiveMessage; return true; }
            if (!AtBeacon) { ActionStatus = returnMessage; return false; }
            activated = true; ActionStatus = successMessage; Present(true); return true;
        }
        public void RestartSurvey()
        {
            if (pause != null && pause.BlocksActions) return;
            foreach (var site in sites) site.ResetSurvey();
            activated = false; Present(false);
            ActionStatus = restartMessage;
        }
        private void Present(bool active)
        {
            if (beaconIndicator == null || (properties != null && active == presentedActive)) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            presentedActive = active;
            properties.SetColor("_Color", active ? activeColor : dormantColor);
            properties.SetColor("_EmissionColor", (active ? activeColor : dormantColor) * 0.5f);
            beaconIndicator.SetPropertyBlock(properties);
        }
    }
}
