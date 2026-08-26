using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// The player's brain. Reads input, turns it into a world-space intent using the camera as
    /// the frame of reference, asks the stamina pool whether sprinting is affordable, and drives
    /// movement. It is the only place that knows about all three.
    /// <para>
    /// Deliberately thin. Everything it coordinates is testable on its own, and a future system
    /// (climbing, gliding, being staggered by a mutant) plugs in here rather than inside
    /// locomotion.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Frame of reference for movement. Falls back to the main camera.")]
        [SerializeField] Transform _cameraTransform;

        [Tooltip("Optional. Without one, sprinting is free.")]
        [SerializeField] StaminaSystem _stamina;

        [Header("Sprint")]
        [Tooltip("Below this much movement input, holding sprint does nothing and costs nothing.")]
        [Range(0f, 1f)][SerializeField] float _sprintInputThreshold = 0.4f;

        [SerializeField] LegacyInputProvider _input = new LegacyInputProvider();

        PlayerMovement _movement;

        public IInputProvider Input => _input;
        public PlayerMovement Movement => _movement;
        public StaminaSystem Stamina => _stamina;

        /// <summary>Where the player is trying to go, in world space, this frame.</summary>
        public Vector3 MoveIntent { get; private set; }

        void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            if (_stamina == null) _stamina = GetComponent<StaminaSystem>();
            if (_cameraTransform == null && Camera.main != null) _cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            Vector2 move = _input.Move;
            MoveIntent = ToWorldDirection(move);

            bool wantsSprint = _input.SprintHeld && move.magnitude >= _sprintInputThreshold;
            bool sprinting = wantsSprint && (_stamina == null || _stamina.CanSpend);

            // Ticked whether or not it is being spent: regeneration is its business too.
            if (_stamina != null) _stamina.Tick(deltaTime, sprinting);

            if (_input.JumpPressed) _movement.RequestJump();
            _movement.SetJumpHeld(_input.JumpHeld);

            _movement.Tick(MoveIntent, sprinting, deltaTime);
        }

        /// <summary>
        /// Projects input into the camera's frame, flattened onto the ground plane. Flattening
        /// matters: without it, looking down at the terrain would quietly slow the player down.
        /// </summary>
        Vector3 ToWorldDirection(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;

                // Straight down or straight up leaves nothing to project. Keep the last sane
                // basis rather than snapping the player to a world axis.
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.Cross(right, Vector3.up).normalized;
            }

            Vector3 direction = forward * move.y + right * move.x;
            return Vector3.ClampMagnitude(direction, 1f);
        }
    }
}
