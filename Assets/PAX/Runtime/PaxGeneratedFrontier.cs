using UnityEngine;

namespace Pax
{
    // Saved provenance/references only. Editing a definition never regenerates a loaded scene.
    public sealed class PaxGeneratedFrontier : MonoBehaviour
    {
        [SerializeField] private PaxFrontierDefinition definition;
        [SerializeField] private Transform spawnMarker, nearBank, farBank;
        [SerializeField] private PaxExpedition mission;
        [SerializeField] private PaxResourceCache cache;
        [SerializeField] private int authoredSeed, authoredTileCount;
        [SerializeField, TextArea] private string authoredSettings;
        [SerializeField] private PaxGeneratedStructure[] structures;
        public PaxFrontierDefinition Definition => definition;
        public Transform SpawnMarker => spawnMarker;
        public Transform NearBank => nearBank;
        public Transform FarBank => farBank;
        public PaxExpedition Mission => mission;
        public PaxResourceCache Cache => cache;
        public int AuthoredSeed => authoredSeed;
        public int AuthoredTileCount => authoredTileCount;
        public PaxGeneratedStructure[] Structures => structures;
        public void Configure(PaxFrontierDefinition source, PaxFrontierLayout layout, Transform spawn, Transform near, Transform far, PaxExpedition objective, PaxResourceCache supplies)
        {
            definition = source; spawnMarker = spawn; nearBank = near; farBank = far; mission = objective; cache = supplies;
            authoredSeed = source.terrain.seed; authoredTileCount = layout.Tiles.Count; structures = layout.Structures;
            authoredSettings = JsonUtility.ToJson(source) + "\nTerrain: " + JsonUtility.ToJson(source.terrain);
        }
        private void OnDrawGizmosSelected()
        {
            if (nearBank == null || farBank == null || mission == null) return;
            Gizmos.color = Color.cyan; Gizmos.DrawLine(nearBank.position, farBank.position); Gizmos.DrawLine(farBank.position, mission.BeaconReturn.position);
        }
    }
}
