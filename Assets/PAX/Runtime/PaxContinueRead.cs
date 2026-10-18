using System;

namespace Pax
{
    public sealed class PaxContinueCandidate
    {
        public PaxResourceSnapshot Data { get; }
        public bool Previous { get; }
        public PaxContinueCandidate(PaxResourceSnapshot data, bool previous) { Data = data; Previous = previous; }
    }
    public static class PaxContinueRead
    {
        // Read-only selection. Never replaces a primary, creates a backup or repairs a file.
        public static bool Best(string primaryPath, Func<PaxResourceSnapshot, bool> compatible, out PaxContinueCandidate candidate, out string reason)
        {
            candidate = null;
            if (Read(primaryPath, compatible, out var data, out string primaryReason)) { candidate = new PaxContinueCandidate(data, false); reason = "Compatible current snapshot. Journal starts fresh."; return true; }
            if (Read(primaryPath + ".bak", compatible, out data, out string backupReason)) { candidate = new PaxContinueCandidate(data, true); reason = "Recoverable previous snapshot; current unavailable. Journal starts fresh."; return true; }
            reason = "No compatible snapshot. Current: " + primaryReason + " Previous: " + backupReason + " Start fresh to play."; return false;
        }
        private static bool Read(string path, Func<PaxResourceSnapshot, bool> compatible, out PaxResourceSnapshot data, out string reason)
        {
            data = null;
            try
            {
                var parsed = PaxResourceSnapshot.Decode(PaxAtomicFile.Read(path));
                if (!compatible(parsed)) { reason = "world/rules/resources do not match."; return false; }
                data = parsed; reason = null; return true;
            }
            catch (Exception error) when (PaxSaveFile.IsExpectedFailure(error)) { reason = error.Message; return false; }
        }
    }
}
