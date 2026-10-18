using UnityEngine;

namespace Pax
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PaxPlayer : MonoBehaviour
    {
        [SerializeField] private PaxPause pause;
        public void UsePause(PaxPause owner) => pause = owner;
        [SerializeField] private CharacterController body;
        [SerializeField] private Transform movementFrame;
        [SerializeField] private Transform respawn;
        [SerializeField, Min(0.1f)] private float speed = 4.5f;
        [SerializeField, Min(1)] private float gravity = 24;
        [SerializeField, Range(0.25f, 2)] private float jumpHeight = 2;
        [SerializeField, Range(0, 1)] private float releaseMultiplier = 0.4f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBuffer = 0.12f;
        [SerializeField] private float respawnBelow = -8;
        private Vector2 move;
        private bool pressed, held;
        private float verticalSpeed, lastGrounded = -10, lastJump = -10;
        private bool jumping, jumpCut;
        public bool Grounded => body.isGrounded;
        public float StableFeetHeight { get; private set; }
        public Vector3 Chest => transform.position + Vector3.up;
        public Bounds BodyBounds => body.bounds;
        public float VerticalSpeed => verticalSpeed;
        public void ClearCommand() { move = Vector2.zero; pressed = held = false; }

        public void Configure(CharacterController controller, Transform cameraFrame, Transform spawn)
        {
            body = controller;
            movementFrame = cameraFrame;
            respawn = spawn;
        }
        private void Awake() => StableFeetHeight = transform.position.y;
        public void SetCommand(Vector2 movement, bool jumpPressed, bool jumpHeld)
        {
            if (pause != null && pause.BlocksActions) return;
            move = Vector2.ClampMagnitude(movement, 1);
            pressed |= jumpPressed;
            held = jumpHeld;
        }

        private void Update()
        {
            if (pause != null && pause.IsPaused) return;
            float dt = Time.deltaTime;
            if (Grounded && verticalSpeed <= 0)
            {
                verticalSpeed = -2;
                StableFeetHeight = transform.position.y;
                lastGrounded = Time.time;
                jumping = false;
            }
            if (pressed) lastJump = Time.time;
            pressed = false;
            if (!jumping && Time.time - lastGrounded <= coyoteTime && Time.time - lastJump <= jumpBuffer)
            {
                verticalSpeed = Mathf.Sqrt(2 * gravity * jumpHeight);
                lastJump = -10;
                lastGrounded = -10;
                jumping = true;
                jumpCut = false;
            }
            if (jumping && !held && !jumpCut && verticalSpeed > 0)
            {
                verticalSpeed *= releaseMultiplier;
                jumpCut = true;
            }
            Vector3 forward = Vector3.ProjectOnPlane(movementFrame.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 displacement = (right * move.x + forward * move.y) * (speed * dt);
            displacement.y = verticalSpeed * dt - 0.5f * gravity * dt * dt;
            verticalSpeed -= gravity * dt;
            CollisionFlags collision = body.Move(displacement);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if (transform.position.y < respawnBelow) Warp(respawn.position);
        }

        public void Warp(Vector3 position)
        {
            if (pause != null && pause.BlocksActions) return;
            body.enabled = false;
            transform.position = position;
            body.enabled = true;
            verticalSpeed = 0;
            lastGrounded = lastJump = -10;
            pressed = held = jumping = jumpCut = false;
            move = Vector2.zero;
            StableFeetHeight = position.y;
        }
    }
}
