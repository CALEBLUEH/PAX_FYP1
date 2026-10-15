using UnityEngine;

namespace Pax
{
    public sealed class PaxCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera view;
        [SerializeField] private float yaw = 45;
        [SerializeField, Range(20, 65)] private float pitch = 35.264f;
        [SerializeField] private float distance = 24;
        [SerializeField] private float followSharpness = 12;
        [SerializeField] private Vector2 zoomLimits = new Vector2(5, 18);
        private float defaultYaw, defaultPitch, defaultZoom;
        public Camera View => view;
        public void Configure(Transform followTarget, Camera camera)
        {
            target = followTarget;
            view = camera;
        }
        private void Awake()
        {
            defaultYaw = yaw;
            defaultPitch = pitch;
            defaultZoom = view.orthographicSize;
        }
        public void Orbit(float horizontal, float vertical)
        {
            yaw += horizontal;
            pitch = Mathf.Clamp(pitch + vertical, 20, 65);
        }
        public void Zoom(float amount) => view.orthographicSize = Mathf.Clamp(view.orthographicSize - amount, zoomLimits.x, zoomLimits.y);
        public void ResetView()
        {
            yaw = defaultYaw; pitch = defaultPitch; view.orthographicSize = defaultZoom;
        }
        public void SnapToTarget()
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            transform.position = target.position + Vector3.up - transform.forward * distance;
        }
        private void LateUpdate()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            float blend = 1 - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
            Vector3 destination = target.position + Vector3.up - transform.forward * distance;
            transform.position = Vector3.Lerp(transform.position, destination, blend);
        }
    }
}
