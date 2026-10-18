using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pax
{
    // One short-lived explicit Continue request. No profile, serialized player state or autosave.
    public sealed class PaxContinueTransfer : MonoBehaviour
    {
        [SerializeField] private PaxContinueCatalog catalog;
        private PaxContinueSlot slot; private string validationDirectory; private bool validationInput;
        private PaxContinueReceiver receiver;
        public string ScenePath => slot?.scenePath;
        public bool ValidationInput => validationInput;
        public void Configure(PaxContinueCatalog source, PaxContinueSlot selected, string directory, bool useValidationInput)
        { catalog = source; slot = selected; validationDirectory = directory; validationInput = useValidationInput; DontDestroyOnLoad(gameObject); }
        private IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync(slot.scenePath, LoadSceneMode.Single);
            receiver = FindFirstObjectByType<PaxContinueReceiver>();
            if (receiver != null && receiver.gameObject.scene.path == slot.scenePath)
                yield return receiver.Restore(slot, validationDirectory, validationInput);
            else
                FindFirstObjectByType<PaxPlaytestSave>()?.ReportContinue("Continue unavailable in this scene. Fresh world kept; use F9 manually or return to selection.");
            Destroy(gameObject);
        }
        private void OnDisable() { if (receiver != null) receiver.ReleaseHold(); }
    }
}
