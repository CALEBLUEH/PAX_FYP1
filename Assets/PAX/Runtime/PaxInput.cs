using UnityEngine;

namespace Pax
{
    [DefaultExecutionOrder(-100)]
    public sealed class PaxInput : MonoBehaviour
    {
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
            player.SetCommand(new Vector2((Input.GetKey(right) ? 1 : 0) - (Input.GetKey(left) ? 1 : 0),
                (Input.GetKey(forward) ? 1 : 0) - (Input.GetKey(backward) ? 1 : 0)), Input.GetKeyDown(jump), Input.GetKey(jump));
            float orbit = ((Input.GetKey(rotateRight) ? 1 : 0) - (Input.GetKey(rotateLeft) ? 1 : 0)) * rotateSpeed * Time.deltaTime;
            float tilt = 0;
            if (Input.GetMouseButton(2))
            {
                orbit += Input.GetAxisRaw("Mouse X") * mouseOrbitSpeed;
                tilt -= Input.GetAxisRaw("Mouse Y") * mouseOrbitSpeed;
            }
            cameraRig.Orbit(orbit, tilt);
            cameraRig.Zoom(Input.mouseScrollDelta.y * zoomSpeed);
            if (Input.GetKeyDown(resetCamera)) cameraRig.ResetView();
            if (Input.GetKeyDown(toggleCursor)) builder.ToggleCursor();
            if (Input.GetKeyDown(toggleGhostTargets)) builder.ToggleGhostTargets();
            if (Input.GetKeyDown(respawn)) player.Warp(spawn.position);
            if (Input.GetKeyDown(saveEdits)) playtestSave.SaveEdits();
            else if (Input.GetKeyDown(loadEdits)) playtestSave.LoadEdits();
            else if (Input.GetKeyDown(loadPrevious)) playtestSave.LoadEdits(true);
            builder.SetPointer(Input.mousePosition, Input.GetMouseButtonDown(0), Input.GetMouseButtonDown(1));
        }
        private void OnDisable() { if (player != null) player.SetCommand(Vector2.zero, false, false); }
        private void OnApplicationFocus(bool focus) { if (!focus) player.SetCommand(Vector2.zero, false, false); }
    }
}
