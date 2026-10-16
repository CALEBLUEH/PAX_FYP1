using UnityEngine;

namespace Pax
{
    // Provenance and editable markers only: changing the definition never rewrites a saved world.
    public sealed class PaxGeneratedIsland : MonoBehaviour
    {
        [SerializeField] private PaxGenerationDefinition definition;
        [SerializeField] private Transform spawnMarker;
        [SerializeField] private int authoredSeed;
        [SerializeField] private int authoredTileCount;
        [SerializeField] private string authoredSettings;
        [SerializeField] private PaxGeneratedStructure[] structures;
        public PaxGenerationDefinition Definition => definition;
        public Transform SpawnMarker => spawnMarker;
        public int AuthoredSeed => authoredSeed;
        public int AuthoredTileCount => authoredTileCount;
        public PaxGeneratedStructure[] Structures => structures;

        public void Configure(PaxGenerationDefinition source, PaxIslandLayout layout, Transform spawn)
        {
            definition = source; spawnMarker = spawn; authoredSeed = source.seed;
            authoredSettings = JsonUtility.ToJson(source);
            authoredTileCount = layout.Tiles.Count; structures = layout.Structures.ToArray();
        }
    }
}
