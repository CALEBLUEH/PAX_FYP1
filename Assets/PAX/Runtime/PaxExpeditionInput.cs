using UnityEngine;

namespace Pax
{
    public sealed class PaxExpeditionInput : MonoBehaviour
    {
        [SerializeField] private PaxExpedition expedition;
        [SerializeField] private KeyCode activate = KeyCode.F, restartSurvey = KeyCode.F6;
        public void Configure(PaxExpedition objective) => expedition = objective;
        private void Update()
        {
            if (Input.GetKeyDown(restartSurvey)) expedition.RestartSurvey();
            else if (Input.GetKeyDown(activate)) expedition.TryActivate();
        }
    }
}
