using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PaxExpeditionHud : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private PaxExpedition expedition;
        [SerializeField] private PaxPlayer player;
        [SerializeField, Min(0.03f)] private float refreshInterval = 0.1f;
        [SerializeField] private string surveyedLabel = "Surveyed", unreadLabel = "Unread";
        [SerializeField] private string progressFormat = "Survey {0}/{1}  ·  Solid platform cells {2}/{3}";
        private Label title, introduction, objective, sites, repairs, action;
        private float nextRefresh;
        private readonly StringBuilder siteText = new StringBuilder();
        public void Configure(UIDocument ui, PaxExpedition mission, PaxPlayer motor) { document = ui; expedition = mission; player = motor; }
        private void OnEnable()
        {
            var root = document.rootVisualElement;
            title = root.Q<Label>("expedition-title"); introduction = root.Q<Label>("expedition-intro");
            objective = root.Q<Label>("expedition-objective"); sites = root.Q<Label>("expedition-sites");
            repairs = root.Q<Label>("expedition-repairs"); action = root.Q<Label>("expedition-action");
        }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh || objective == null) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            title.text = expedition.Title; introduction.text = expedition.Introduction;
            objective.text = expedition.Objective;
            siteText.Clear();
            foreach (var site in expedition.Sites)
            {
                if (siteText.Length > 0) siteText.Append('\n');
                siteText.Append(site.Title).Append(" / ").Append(site.Surveyed ? surveyedLabel : unreadLabel);
                if (site.Surveyed) siteText.Append(" — ").Append(site.FieldNote);
                else siteText.Append(" / ").Append(Vector3.Distance(player.transform.position, site.transform.position).ToString("F0")).Append("m / grid ").Append(Mathf.RoundToInt(site.transform.position.x)).Append(", ").Append(Mathf.RoundToInt(site.transform.position.z));
            }
            sites.text = siteText.ToString();
            repairs.text = string.Format(progressFormat, expedition.SurveyedCount, expedition.Sites.Length, expedition.RepairCount, expedition.Repairs.Length);
            action.text = expedition.ActionStatus;
        }
    }
}
