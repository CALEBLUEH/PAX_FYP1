using UnityEngine;

namespace Pax
{
    // One sampled gameplay frame; raw device reads remain in PaxInput.
    public struct PaxInputFrame
    {
        public Vector2 movement, pointer;
        public float orbit, tilt, zoom;
        public bool jumpPressed, jumpHeld, mine, place, resetCamera, toggleCursor, toggleGhostTargets, respawn;
        public bool save, load, previous;
    }
}
