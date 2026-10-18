using System.Collections;
using UnityEngine;

namespace Pax
{
    [DefaultExecutionOrder(-400)]
    public sealed class PaxContinueReceiver : MonoBehaviour
    {
        [SerializeField] private PaxPlaytestSave storage;
        [SerializeField] private PaxStockpile stockpile;
        [SerializeField] private PaxWorld world;
        [SerializeField] private PaxPause pause;
        private bool held;
        public bool Finished { get; private set; }
        public bool Restored { get; private set; }
        public void Configure(PaxPlaytestSave save, PaxStockpile stocks, PaxWorld grid, PaxPause owner)
        { storage = save; stockpile = stocks; world = grid; pause = owner; }
        private void Awake()
        {
            var pending = FindFirstObjectByType<PaxContinueTransfer>();
            if (pending != null && pending.ScenePath == gameObject.scene.path) { pause.HoldContinue(true); held = true; if (pending.ValidationInput) pause.UseValidationInput(); }
        }
        internal IEnumerator Restore(PaxContinueSlot selected, string directory, bool validationInput)
        {
            pause.HoldContinue(true); held = true;
            if (directory != null) storage.UseValidationDirectory(directory);
            storage.ReportContinue("Continue selected. Rechecking this expedition's world, stocks and harvest history…");
            float deadline = Time.realtimeSinceStartup + 15;
            while (stockpile != null && !stockpile.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            if (stockpile == null || !stockpile.Ready) { storage.ReportContinue("Continue unavailable: expedition setup is not ready. Fresh world kept."); Finish(); yield break; }
            while (this != null && pause != null && !pause.CanApplyContinue)
            { storage.ReportContinue("Continue is waiting. Return to this window and explicitly Resume; saved files are kept."); yield return null; }
            if (this == null || pause == null) yield break;
            if (gameObject.scene.path != selected.scenePath || storage.SaveFolder != selected.folder || storage.SaveFile != selected.file || world.SaveSignature != selected.SaveSignature || stockpile.Rules.Signature(stockpile.Palette) != selected.rules.Signature(selected.palette))
                storage.ReportContinue("Continue rejected: this expedition no longer matches the selected saved-world definition. Fresh world and saved files kept. Start fresh or return to selection.");
            else Restored = storage.ContinueSaved();
            Finish();
        }
        private void Finish() { Finished = true; ReleaseHold(); }
        internal void ReleaseHold() { if (held && pause != null) pause.HoldContinue(false); held = false; }
        private void OnDisable() => ReleaseHold();
    }
}
