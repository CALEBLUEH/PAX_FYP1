using System;
using UnityEngine;

namespace Pax
{
    [CreateAssetMenu(menuName = "PAX/Construction material palette")]
    public sealed class PaxMaterialPalette : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string title;
            public Material material;
        }
        [SerializeField] private string paletteTitle = "Construction / Unlimited supply";
        [SerializeField] private Entry[] entries;
        public string Title => paletteTitle;
        public System.Collections.Generic.IReadOnlyList<Entry> Entries => entries;
        public void Configure(Entry[] choices) => entries = choices;
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 32 || id[0] < 'a' || id[0] > 'z') return false;
            foreach (char c in id) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_') return false;
            return true;
        }
        public bool Validate(out string reason)
        {
            reason = null;
            if (entries == null || entries.Length < 1 || entries.Length > 9) { reason = "Palette requires 1-9 choices."; return false; }
            bool hasDefault = false;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || !IsValidId(entry.id) || string.IsNullOrWhiteSpace(entry.title) || entry.material == null)
                { reason = "Palette choice needs a valid stable ID, title and material."; return false; }
                if (!entry.material.HasProperty("_Visibility") || !entry.material.HasProperty("_GhostRelevance") || !entry.material.HasProperty("_WorldSunlight"))
                { reason = "Palette materials must support the PAX cutaway and sunlight properties."; return false; }
                hasDefault |= entry.id == "default";
                for (int j = 0; j < i; j++)
                    if (entries[j].id == entry.id || entries[j].material == entry.material)
                    { reason = "Palette IDs and material references must be unique."; return false; }
            }
            if (!hasDefault) { reason = "Palette must retain the reserved default ID for legacy saves."; return false; }
            return true;
        }
        public bool TryResolve(string id, out Material material)
        {
            material = null;
            if (!Validate(out _)) return false;
            foreach (var entry in entries) if (entry.id == id) { material = entry.material; return true; }
            return false;
        }
        public bool TryIdentify(Material material, out string id)
        {
            id = null;
            if (!Validate(out _)) return false;
            foreach (var entry in entries) if (entry.material == material) { id = entry.id; return true; }
            return false;
        }
    }
}
