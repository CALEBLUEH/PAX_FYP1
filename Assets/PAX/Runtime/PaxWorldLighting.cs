using UnityEngine;

namespace Pax
{
    // Coarse directional tile sunlight is calculated from physical solids, never camera opacity.
    // Transparent surfaces cannot rely on Built-in's opaque screen-space shadow receiver depth.
    public sealed class PaxWorldLighting : MonoBehaviour
    {
        [SerializeField] private PaxWorld world;
        [SerializeField] private Light sun;
        [SerializeField, Min(10)] private float sunlightDistance = 50;
        private int revision=-1;
        private Vector3 lastDirection;
        private readonly RaycastHit[] sunlightHits=new RaycastHit[128];
        public void Configure(PaxWorld grid, Light worldSun) { world=grid; sun=worldSun; }
        private void LateUpdate()
        {
            if(revision!=world.Revision || Vector3.Angle(lastDirection,-sun.transform.forward)>0.1f) RefreshNow();
        }
        public void RefreshNow()
        {
            revision=world.Revision;
            lastDirection=-sun.transform.forward;
            Physics.SyncTransforms();
            foreach(var block in world.Blocks)
            {
                if(block==null) continue;
                int lit=0;
                for(int sample=-1;sample<=1;sample++)
                {
                    Vector3 point=PaxGrid.Centre(block.Cell)+new Vector3(sample*0.3f,0.505f,sample*0.3f);
                    int count=Physics.RaycastNonAlloc(point,lastDirection,sunlightHits,sunlightDistance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    bool blocked=false;
                    for(int hit=0;hit<count;hit++)
                    {
                        var tile=sunlightHits[hit].collider.GetComponent<PaxBlock>();
                        if(tile!=null && world.TryGet(tile.Cell,out var registered) && registered==tile) { blocked=true; break; }
                    }
                    if(!blocked) lit++;
                }
                block.SetSunlight(lit/3f);
            }
        }
    }
}
