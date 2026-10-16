using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Pax
{
    [CreateAssetMenu(menuName = "PAX/Finite resource rules")]
    public sealed class PaxResourceRules : ScriptableObject
    {
        public const int MaximumUnits = 10000;
        [Serializable] public sealed class Entry { public string id; public int start; public int authoredYield = 1; public int limit = 64; }
        [SerializeField] private Entry[] entries;
        public System.Collections.Generic.IReadOnlyList<Entry> Entries => entries;
        public void Configure(Entry[] values) => entries = values;
        public bool Validate(PaxMaterialPalette palette, out string reason)
        {
            reason = null;
            if (palette == null || !palette.Validate(out reason) || entries == null || entries.Length != palette.Entries.Count) { reason = "Rules must map every palette material exactly once."; return false; }
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || !palette.TryResolve(e.id, out _) || e.start < 0 || e.limit < 1 || e.limit > MaximumUnits || e.start > e.limit || e.authoredYield < 0 || e.authoredYield > e.limit || entries.Take(i).Any(other => other.id == e.id))
                { reason = "Invalid/duplicate resource ID, count, yield or limit."; return false; }
            }
            return true;
        }
        public string Signature(PaxMaterialPalette palette)
        {
            if (!Validate(palette, out string reason)) throw new System.IO.InvalidDataException(reason);
            string text = "finite-v1/cost=1/placed-refund=1\n" + string.Join("\n", entries.OrderBy(e => e.id, StringComparer.Ordinal).Select(e =>
            {
                palette.TryResolve(e.id, out var material);
                return FormattableString.Invariant($"{e.id}:{material.name}:{e.start}:{e.authoredYield}:{e.limit}");
            }));
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
    }
}
