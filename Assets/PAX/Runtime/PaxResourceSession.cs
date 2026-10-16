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
