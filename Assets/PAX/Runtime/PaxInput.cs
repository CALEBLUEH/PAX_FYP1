using UnityEngine;

namespace Pax
{
    [DefaultExecutionOrder(-100)]
    public sealed class PaxInput : MonoBehaviour
    {
        [SerializeField] private PaxPause pause;
        public void UsePause(PaxPause owner) => pause = owner;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private PaxCamera cameraRig;
        [SerializeField] private PaxBuilder builder;
        [SerializeField] private KeyCode forward = KeyCode.W, backward = KeyCode.S, left = KeyCode.A, right = KeyCode.D;
        [SerializeField] private KeyCode jump = KeyCode.Space, toggleCursor = KeyCode.LeftControl;
        [SerializeField] private KeyCode toggleGhostTargets = KeyCode.G;
        [SerializeField] private KeyCode rotateLeft = KeyCode.Q, rotateRight = KeyCode.E, resetCamera = KeyCode.Home, respawn = KeyCode.R;
        [SerializeField] private Transform spawn;
        [SerializeField] private PaxPlaytestSave playtestSave;
        [SerializeField] private KeyCode saveEdits = KeyCode.F5, loadEdits = KeyCode.F9, loadPrevious = KeyCode.F8;
        [SerializeField] private float rotateSpeed = 90, mouseOrbitSpeed = 3, zoomSpeed = 2;
        public void Configure(PaxPlayer motor, PaxCamera rig, PaxBuilder interaction, Transform spawnPoint)
        {
            player = motor; cameraRig = rig; builder = interaction; spawn = spawnPoint;
        }
        public void ConfigureSave(PaxPlaytestSave session) => playtestSave = session;
        private void Update()
        {
            if (pause != null && pause.BlocksActions) return;
            float orbit = ((Input.GetKey(rotateRight) ? 1 : 0) - (Input.GetKey(rotateLeft) ? 1 : 0)) * rotateSpeed * Time.deltaTime;
            float tilt = 0;
            if (Input.GetMouseButton(2))
            {
                orbit += Input.GetAxisRaw("Mouse X") * mouseOrbitSpeed;
                tilt -= Input.GetAxisRaw("Mouse Y") * mouseOrbitSpeed;
            }
            ApplyFrame(new PaxInputFrame {
                movement = new Vector2((Input.GetKey(right) ? 1 : 0) - (Input.GetKey(left) ? 1 : 0), (Input.GetKey(forward) ? 1 : 0) - (Input.GetKey(backward) ? 1 : 0)),
                jumpPressed = Input.GetKeyDown(jump), jumpHeld = Input.GetKey(jump), orbit = orbit, tilt = tilt, zoom = Input.mouseScrollDelta.y * zoomSpeed,
                resetCamera = Input.GetKeyDown(resetCamera), toggleCursor = Input.GetKeyDown(toggleCursor), toggleGhostTargets = Input.GetKeyDown(toggleGhostTargets), respawn = Input.GetKeyDown(respawn),
                save = Input.GetKeyDown(saveEdits), load = Input.GetKeyDown(loadEdits), previous = Input.GetKeyDown(loadPrevious),
                pointer = Input.mousePosition, mine = Input.GetMouseButtonDown(0), place = Input.GetMouseButtonDown(1) });
        }
        public void ApplyFrame(PaxInputFrame frame)
        {
            if (pause != null && pause.BlocksActions) return;
            player.SetCommand(frame.movement, frame.jumpPressed, frame.jumpHeld);
            cameraRig.Orbit(frame.orbit, frame.tilt); cameraRig.Zoom(frame.zoom);
            if (frame.resetCamera) cameraRig.ResetView();
            if (frame.toggleCursor) builder.ToggleCursor();
            if (frame.toggleGhostTargets) builder.ToggleGhostTargets();
            if (frame.respawn) player.Warp(spawn.position);
            if (frame.save) playtestSave.SaveEdits(); else if (frame.load) playtestSave.LoadEdits(); else if (frame.previous) playtestSave.LoadEdits(true);
            builder.SetPointer(frame.pointer, frame.mine, frame.place);
        }
        private void OnDisable() { if (player != null) player.ClearCommand(); if (builder != null) builder.ClearPendingActions(); }
        private void OnApplicationFocus(bool focus) { if (!focus && player != null) player.ClearCommand(); }
    }
}
