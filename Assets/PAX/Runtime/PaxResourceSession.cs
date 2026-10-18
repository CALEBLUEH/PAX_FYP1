using System;
using UnityEngine;

namespace Pax
{
    [DefaultExecutionOrder(-140)]
    public sealed class PaxResourceSession : MonoBehaviour
    {
        [SerializeField] private PaxWorld world;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private PaxStockpile stockpile;
        [SerializeField] private PaxPlaytestSave storage;
        public void Configure(PaxWorld grid, PaxPlayer motor, PaxStockpile resources, PaxPlaytestSave save)
        { world = grid; player = motor; stockpile = resources; storage = save; }
        private void Awake() => storage.UseResourceSession(this);
        internal bool Continue(out string status)
        {
            if (!stockpile.Ready) { status = "Continue unavailable: resource setup is not ready. Fresh world kept."; return false; }
            if (!PaxContinueRead.Best(storage.SavePath, data => stockpile.Validate(data, out _), out var candidate, out string reason))
            { status = "Continue rejected; fresh world kept. " + reason; return false; }
            // A valid primary blocked by the authored spawn must not silently fall back to older data.
            if (!world.TryApplyEdits(candidate.Data.Tiles, player.BodyBounds, out reason))
            { status = "Continue rejected; fresh world kept. " + reason + " Use F9 after stepping aside, or F8 for a previous snapshot."; return false; }
            stockpile.RestoreValidated(candidate.Data);
            status = (candidate.Previous ? "Continued previous compatible snapshot; current file kept. " : "Continued current snapshot. ") + "World + stocks + harvest history restored at camp; session journal starts fresh. No file written.";
            return true;
        }
        public bool Save(out string status)
        {
            try
            {
                string text = stockpile.Capture().Encode();
                PaxAtomicFile.Write(storage.SavePath, text, candidate => stockpile.Validate(PaxResourceSnapshot.Decode(candidate), out _));
                status = "Stockpile + tiles saved together. F9 loads; F8 recovers the previous snapshot."; return true;
            }
            catch (Exception error) when (PaxSaveFile.IsExpectedFailure(error)) { status = "Resource save rejected; files kept. " + error.Message; return false; }
        }
        public bool Load(bool previous, out string status)
        {
            try
            {
                var data = PaxResourceSnapshot.Decode(PaxAtomicFile.Read(previous ? storage.BackupPath : storage.SavePath));
                if (!stockpile.Validate(data, out string reason) || !world.TryApplyEdits(data.Tiles, player.BodyBounds, out reason))
                { status = "Resource load rejected; stockpile and world kept. " + reason; return false; }
                stockpile.RestoreValidated(data);
                status = "Stockpile + tiles restored together. Survey visits remain session-only."; return true;
            }
            catch (Exception error) when (PaxSaveFile.IsExpectedFailure(error)) { status = "Resource load failed; stockpile and world kept. " + error.Message; return false; }
        }
    }
}
