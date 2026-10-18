using UnityEngine;

namespace Pax
{
    public sealed class PaxExpeditionInput : MonoBehaviour
    {
        [SerializeField] private PaxPause pause;
        public void UsePause(PaxPause owner) => pause = owner;
        [SerializeField] private PaxExpedition expedition;
        [SerializeField] private KeyCode activate = KeyCode.F, restartSurvey = KeyCode.F6;
        public void Configure(PaxExpedition objective) => expedition = objective;
        private void Update()
        {
            if (pause != null && pause.BlocksActions) return;
            if (Input.GetKeyDown(restartSurvey)) expedition.RestartSurvey();
            else if (Input.GetKeyDown(activate)) expedition.TryActivate();
        }
    }
}
