using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pax
{
    // Authored route/repair guide. Progress remains owned by the existing mission/markers.
    public sealed class PaxChartRoute : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private PaxRepairMarker[] timberApproach;
        [SerializeField] private PaxRepairMarker[] stoneLookout;
        [SerializeField] private Transform departure, nearBank, mooring, takeoff, firstLanding, upperLanding, chart;
        [SerializeField, TextArea] private string guide = "Amber outlines require Timber (2); cyan outlines require Stone (3). Wrong material? Mine it back and replace it. If you fall, R returns to camp.";
        public PaxRepairMarker[] Timber => timberApproach;
        public PaxRepairMarker[] Stone => stoneLookout;
        public Transform Departure => departure;
        public Transform NearBank => nearBank;
        public Transform Mooring => mooring;
        public Transform Takeoff => takeoff;
        public Transform FirstLanding => firstLanding;
        public Transform UpperLanding => upperLanding;
        public Transform Chart => chart;
        public int TimberCount => timberApproach.Count(marker => marker.IsSolid);
        public int StoneCount => stoneLookout.Count(marker => marker.IsSolid);
        private float nextRefresh;
        public void Configure(UIDocument ui, PaxRepairMarker[] timber, PaxRepairMarker[] stone, Transform[] route)
        { document=ui; timberApproach=timber; stoneLookout=stone; departure=route[0]; nearBank=route[1]; mooring=route[2]; takeoff=route[3]; firstLanding=route[4]; upperLanding=route[5]; chart=route[6]; }
        private void Update()
        {
            if(Time.unscaledTime<nextRefresh) return; nextRefresh=Time.unscaledTime+0.1f;
            var label=document.rootVisualElement.Q<Label>("chart-route");
            if(label!=null) label.text=$"Timber approach {TimberCount}/{timberApproach.Length}  ·  Stone lookout {StoneCount}/{stoneLookout.Length}\n{guide}";
        }
        private void OnDrawGizmosSelected()
        {
            if(departure==null||nearBank==null||mooring==null||takeoff==null||firstLanding==null||upperLanding==null||chart==null) return;
            Gizmos.color=Color.yellow;
            var points=new[]{departure,nearBank,mooring,takeoff,firstLanding,upperLanding,chart};
            for(int i=0;i<points.Length;i++){Gizmos.DrawWireSphere(points[i].position,0.2f);if(i>0)Gizmos.DrawLine(points[i-1].position,points[i].position);}
        }
    }
}
