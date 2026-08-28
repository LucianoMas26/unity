using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// Third-person orbit camera: slightly behind and above the character, aimed at the upper
    /// torso, following with a little damping.
    /// <para>
    /// The mouse owns the camera outright and the character is not allowed to drag it around.
    /// That is what lets the player look one way and walk another, and it is the difference
    /// between exploring a place and being steered through it.
    /// </para>
    /// <para>
    /// Position is smoothed; rotation is not. Damping the rotation of a mouse-driven camera is
    /// the single most common cause of a view that feels like it is lagging behind the hand, so
    /// <see cref="_rotationSmoothing"/> exists but defaults to off.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform _target;

        [Tooltip("Height above the target's feet that the camera aims at. Upper torso, so the " +
                 "character sits low in frame and the ground ahead gets the rest of the screen.")]
        [SerializeField] float _pivotHeight = 1.5f;

        [Header("Distance")]
        [SerializeField] float _distance = 5f;
        [SerializeField] float _minDistance = 2.5f;
        [SerializeField] float _maxDistance = 7f;

        [Header("Orbit")]
        [Tooltip("Starting pitch in degrees. Positive looks down at the ground.")]
        [SerializeField] float _startPitch = 15f;

        [Tooltip("Looking up. Kept shallow: there is nothing above but sky.")]
        [SerializeField] float _minPitch = -25f;

        [Tooltip("Looking down. Steep enough to see straight down a cliff.")]
        [SerializeField] float _maxPitch = 65f;

        [Header("Follow")]
        [Tooltip("Seconds for the camera to catch up to its ideal position. Small values feel " +
                 "attached, large values feel floaty. 0 is rigid.")]
        [SerializeField] float _positionSmoothing = 0.055f;

        [Tooltip("Seconds of damping on the camera's rotation. Leave at 0. Anything above about " +
                 "0.03 reads as mouse lag rather than as smoothness.")]
        [SerializeField] float _rotationSmoothing;

        [Header("Cursor")]
        [Tooltip("Capture the cursor on start. Escape releases it, clicking takes it back.")]
        [SerializeField] bool _captureCursor = true;

        [SerializeField] LegacyInputProvider _input = new LegacyInputProvider();

        CameraCollision _collision;
        float _yaw;
        float _pitch;
        float _smoothedYaw;
        float _smoothedPitch;
        float _yawVelocity;
        float _pitchVelocity;
        Vector3 _followVelocity;
        bool _cursorReleased;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        // Exposed so framing can be tuned live while playing. Getting the feel of a third-person
        // camera right is a matter of trying numbers, not of choosing them on paper.
        public float Distance
        {
            get => _distance;
            set => _distance = Mathf.Clamp(value, _minDistance, _maxDistance);
        }

        public float PivotHeight
        {
            get => _pivotHeight;
            set => _pivotHeight = value;
        }

        public float Pitch
        {
            get => _pitch;
            set => _pitch = Mathf.Clamp(value, _minPitch, _maxPitch);
        }

        public float PositionSmoothing
        {
            get => _positionSmoothing;
            set => _positionSmoothing = Mathf.Max(0f, value);
        }

        public float RotationSmoothing
        {
            get => _rotationSmoothing;
            set => _rotationSmoothing = Mathf.Max(0f, value);
        }

        /// <summary>Height of the camera above the target's feet. The number the brief cares
        /// about, derived rather than set: it falls out of pitch and distance.</summary>
        public float HeightAboveFeet => _pivotHeight + Mathf.Sin(_smoothedPitch * Mathf.Deg2Rad) * _distance;

        void Awake() => _collision = GetComponent<CameraCollision>();

        void Start()
        {
            _pitch = _smoothedPitch = Mathf.Clamp(_startPitch, _minPitch, _maxPitch);
            _yaw = _smoothedYaw = _target != null ? _target.eulerAngles.y : transform.eulerAngles.y;

            SetCursorLocked(_captureCursor);
            SnapToTarget();
        }

        void LateUpdate()
        {
            if (_target == null) return;

            float deltaTime = Time.deltaTime;

            UpdateCursor();
            UpdateOrbit(deltaTime);

            Quaternion rotation = Quaternion.Euler(_smoothedPitch, _smoothedYaw, 0f);
            Vector3 pivot = _target.position + Vector3.up * _pivotHeight;
            Vector3 directionToCamera = -(rotation * Vector3.forward);

            float distance = _collision != null
                ? _collision.Resolve(pivot, directionToCamera, _distance, deltaTime)
                : _distance;

            Vector3 desiredPosition = pivot + directionToCamera * distance;

            transform.position = _positionSmoothing > 0f
                ? Vector3.SmoothDamp(transform.position, desiredPosition, ref _followVelocity, _positionSmoothing)
                : desiredPosition;

            transform.rotation = rotation;
        }

        void UpdateOrbit(float deltaTime)
        {
            _distance = Mathf.Clamp(_distance + _input.Zoom, _minDistance, _maxDistance);

            if (!_cursorReleased)
            {
                Vector2 look = _input.Look;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch + look.y, _minPitch, _maxPitch);
            }

            if (_rotationSmoothing > 0f)
            {
                _smoothedYaw = Mathf.SmoothDampAngle(_smoothedYaw, _yaw, ref _yawVelocity, _rotationSmoothing);
                _smoothedPitch = Mathf.SmoothDampAngle(_smoothedPitch, _pitch, ref _pitchVelocity, _rotationSmoothing);
            }
            else
            {
                _smoothedYaw = _yaw;
                _smoothedPitch = _pitch;
            }
        }

        void UpdateCursor()
        {
            if (!_captureCursor) return;

            if (Input.GetKeyDown(KeyCode.Escape)) _cursorReleased = true;
            else if (_cursorReleased && Input.GetMouseButtonDown(0)) _cursorReleased = false;

            SetCursorLocked(!_cursorReleased);
        }

        public void SnapToTarget()
        {
            if (_target == null) return;

            _smoothedYaw = _yaw;
            _smoothedPitch = _pitch;

            Quaternion rotation = Quaternion.Euler(_smoothedPitch, _smoothedYaw, 0f);
            Vector3 pivot = _target.position + Vector3.up * _pivotHeight;

            transform.position = pivot - rotation * Vector3.forward * _distance;
            transform.rotation = rotation;

            _followVelocity = Vector3.zero;
            if (_collision != null) _collision.Reset(); // ?. would call into a destroyed component
        }

        void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnDisable() => SetCursorLocked(false);

        void OnValidate()
        {
            _minDistance = Mathf.Max(0.5f, _minDistance);
            _maxDistance = Mathf.Max(_minDistance, _maxDistance);
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            _maxPitch = Mathf.Max(_minPitch, _maxPitch);
        }
    }
}
