using Survival.Core;
using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// Locomotion: turning it into velocity, rotation and a jump. Knows nothing about input,
    /// stamina or cameras -- it is handed a world-space direction and told whether to sprint.
    /// <para>
    /// Tuned for control, not for physics. Acceleration is far higher than anything a body could
    /// produce, the jump is arced by a gravity constant rather than a mass, and the character
    /// keeps most of its steering while airborne. That is the point: the brief was that this
    /// should be fun to drive, not accurate.
    /// </para>
    /// <para>
    /// Has no Update of its own. <see cref="PlayerController"/> ticks it, so the order of
    /// turning, moving and falling is decided in one place and cannot drift.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] float _walkSpeed = 5f;
        [SerializeField] float _sprintSpeed = 9.5f;

        [Tooltip("Metres per second squared. High on purpose: 60 reaches walking speed in under " +
                 "a tenth of a second, which is what 'responsive' actually means in numbers.")]
        [SerializeField] float _acceleration = 60f;

        [Tooltip("Slightly lower than acceleration so stopping reads as deliberate rather than " +
                 "as hitting a wall.")]
        [SerializeField] float _deceleration = 45f;

        [Header("Rotation")]
        [Tooltip("Degrees per second for ordinary course corrections.")]
        [SerializeField] float _turnSpeed = 540f;

        [Tooltip("Degrees per second for hard reversals. Turning speed scales up with the size " +
                 "of the turn, so a 180 snaps round without small corrections feeling twitchy.")]
        [SerializeField] float _fastTurnSpeed = 1080f;

        [Tooltip("Turn angle at which the fast speed is fully applied.")]
        [SerializeField] float _fastTurnAngle = 150f;

        [Header("Jump")]
        [SerializeField] float _jumpHeight = 1.6f;
        [SerializeField] float _gravity = -25f;

        [Tooltip("Extra gravity applied while rising after the jump key is released, so a tap " +
                 "hops and a hold leaps.")]
        [SerializeField] float _lowJumpGravityMultiplier = 2.2f;

        [Tooltip("How much steering survives while airborne. 1 = full control, 0 = committed.")]
        [Range(0f, 1f)][SerializeField] float _airControl = 0.7f;

        [Tooltip("Grace period after walking off an edge during which a jump still counts.")]
        [SerializeField] float _coyoteTime = 0.15f;

        [Tooltip("A jump pressed this long before landing still fires on touchdown.")]
        [SerializeField] float _jumpBuffer = 0.12f;

        [Header("Ground")]
        [Tooltip("Downward force kept on while grounded, so the capsule hugs slopes instead of " +
                 "stepping off them.")]
        [SerializeField] float _groundStick = -4f;

        [SerializeField] float _terminalVelocity = -55f;

        CharacterController _controller;
        ITerrainSampler _terrain;
        Vector3 _planarVelocity;
        float _verticalVelocity;
        float _lastGroundedTime = float.NegativeInfinity;
        float _jumpRequestedTime = float.NegativeInfinity;
        bool _jumpHeld;
        bool _waitingForGround = true;

        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }

        /// <summary>Planar speed in m/s. An Animator blend tree will want this.</summary>
        public float CurrentSpeed => _planarVelocity.magnitude;

        /// <summary>Planar speed as a fraction of sprint speed, 0 to 1.</summary>
        public float NormalisedSpeed => _sprintSpeed > 0f ? Mathf.Clamp01(CurrentSpeed / _sprintSpeed) : 0f;

        public float VerticalVelocity => _verticalVelocity;
        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;

        /// <summary>True while the character is still waiting for streamed terrain to exist
        /// beneath it. Nothing should read its position as meaningful until this clears.</summary>
        public bool IsWaitingForGround => _waitingForGround;

        void Awake() => _controller = GetComponent<CharacterController>();

        /// <summary>Queues a jump. Buffered, so pressing slightly early still works.</summary>
        public void RequestJump() => _jumpRequestedTime = Time.time;

        /// <summary>Whether the jump key is still down. Releasing early cuts the jump short.</summary>
        public void SetJumpHeld(bool held) => _jumpHeld = held;

        /// <summary>
        /// Advances one frame.
        /// </summary>
        /// <param name="worldDirection">Where to go, in world space. Length 0 to 1; the length is
        /// the throttle, so a half-pressed stick walks.</param>
        /// <param name="sprint">Whether sprint speed is allowed this frame. The caller has
        /// already decided whether there is stamina for it.</param>
        public void Tick(Vector3 worldDirection, bool sprint, float deltaTime)
        {
            // The world streams in around the player, so for the first frames there is no
            // collision under its feet. Falling then means falling through the planet.
            if (_waitingForGround && !TryStandOnStreamingGround()) return;

            worldDirection.y = 0f;
            float throttle = Mathf.Clamp01(worldDirection.magnitude);
            Vector3 direction = throttle > 0.001f ? worldDirection / throttle : Vector3.zero;

            UpdateRotation(direction, throttle, deltaTime);
            UpdatePlanarVelocity(direction, throttle, sprint, deltaTime);
            UpdateVerticalVelocity(deltaTime);

            Vector3 motion = _planarVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * deltaTime);

            IsGrounded = _controller.isGrounded;
            if (IsGrounded) _lastGroundedTime = Time.time;
        }

        /// <summary>
        /// Turns towards the direction of travel, faster the further it has to turn. A constant
        /// turn rate either makes reversals sluggish or makes small corrections jitter; scaling
        /// with the angle avoids having to choose.
        /// </summary>
        void UpdateRotation(Vector3 direction, float throttle, float deltaTime)
        {
            if (throttle < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            float angle = Quaternion.Angle(transform.rotation, target);
            float speed = Mathf.Lerp(_turnSpeed, _fastTurnSpeed,
                                     Mathf.InverseLerp(45f, _fastTurnAngle, angle));

            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, speed * deltaTime);
        }

        void UpdatePlanarVelocity(Vector3 direction, float throttle, bool sprint, float deltaTime)
        {
            IsSprinting = sprint && throttle > 0.1f;

            float targetSpeed = (IsSprinting ? _sprintSpeed : _walkSpeed) * throttle;
            Vector3 targetVelocity = direction * targetSpeed;

            bool accelerating = targetVelocity.sqrMagnitude > _planarVelocity.sqrMagnitude;
            float rate = accelerating ? _acceleration : _deceleration;
            if (!_controller.isGrounded) rate *= _airControl;

            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVelocity, rate * deltaTime);
        }

        void UpdateVerticalVelocity(float deltaTime)
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = _groundStick;

            bool withinCoyote = Time.time - _lastGroundedTime <= _coyoteTime;
            bool jumpBuffered = Time.time - _jumpRequestedTime <= _jumpBuffer;

            if (withinCoyote && jumpBuffered)
            {
                // v = sqrt(2gh): the height in the inspector is the height actually reached.
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(_gravity) * _jumpHeight);
                _jumpRequestedTime = float.NegativeInfinity;
                _lastGroundedTime = float.NegativeInfinity;
            }

            // Releasing the key mid-rise pulls the arc down early. It is the cheapest way to make
            // a jump feel like something the player is doing rather than watching.
            float gravity = _gravity;
            if (_verticalVelocity > 0f && !_jumpHeld) gravity *= _lowJumpGravityMultiplier;

            _verticalVelocity = Mathf.Max(_verticalVelocity + gravity * deltaTime, _terminalVelocity);
        }

        /// <summary>
        /// Holds the player at the analytic ground height until real collision exists beneath
        /// them, then hands control back to physics. Height is a pure function of the seed, so
        /// this is exact rather than a guess. Scenes with ordinary colliders skip it entirely.
        /// </summary>
        bool TryStandOnStreamingGround()
        {
            if (_terrain == null && !ServiceRegistry.TryGet(out _terrain))
            {
                _waitingForGround = false; // no streamed world to wait for
                return true;
            }

            Vector3 position = transform.position;

            if (_terrain.HasCollisionAt(position))
            {
                _waitingForGround = false;
                return true;
            }

            position.y = _terrain.SampleHeight(position.x, position.z)
                         + _controller.height * 0.5f + _controller.skinWidth;

            _controller.enabled = false;   // moving a CharacterController directly requires this
            transform.position = position;
            _controller.enabled = true;

            _verticalVelocity = 0f;
            return false;
        }
    }
}
