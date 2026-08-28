using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// Drives the character's Animator from what locomotion actually did this frame.
    /// <para>
    /// One-way on purpose: animation reads movement, never the reverse. Root motion stays off,
    /// so the code owns where the character goes and the animation only has to look right about
    /// it. That is the trade this prototype wants -- responsiveness over footfall accuracy.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        /// <summary>Parameter names, shared with the editor script that builds the controller so
        /// the two cannot drift apart silently.</summary>
        public static class Parameters
        {
            public const string Speed = "Speed";
            public const string Grounded = "Grounded";
            public const string VerticalVelocity = "VerticalVelocity";
        }

        [Tooltip("Animator on the character model. Left empty, it is looked up in the children.")]
        [SerializeField] Animator _animator;

        [Tooltip("Seconds of damping on the speed parameter. Without it the blend snaps between " +
                 "idle, walk and run on every small change of input.")]
        [SerializeField] float _speedDamping = 0.1f;

        PlayerMovement _movement;
        int _speedHash;
        int _groundedHash;
        int _verticalHash;

        void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            _speedHash = Animator.StringToHash(Parameters.Speed);
            _groundedHash = Animator.StringToHash(Parameters.Grounded);
            _verticalHash = Animator.StringToHash(Parameters.VerticalVelocity);

            if (_animator != null) _animator.applyRootMotion = false;
        }

        void Update()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            _animator.SetFloat(_speedHash, _movement.NormalisedSpeed, _speedDamping, Time.deltaTime);
            _animator.SetBool(_groundedHash, _movement.IsGrounded);
            _animator.SetFloat(_verticalHash, _movement.VerticalVelocity);
        }
    }
}
