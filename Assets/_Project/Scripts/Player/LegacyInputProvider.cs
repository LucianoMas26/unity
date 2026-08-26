using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// Reads Unity's built-in Input Manager. Chosen for the prototype because it works in a
    /// freshly created project: no extra package, no Player Settings change, no editor restart.
    /// </summary>
    [System.Serializable]
    public sealed class LegacyInputProvider : IInputProvider
    {
        [Header("Look")]
        [SerializeField] float _lookSensitivity = 2.4f;
        [SerializeField] bool _invertLookY;

        [Tooltip("Metres of camera distance per notch of scroll wheel.")]
        [SerializeField] float _zoomSensitivity = 4f;

        public Vector2 Move
        {
            get
            {
                var move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

                // Clamped rather than normalised: a diagonal must not be faster than a straight
                // line, but a partly pressed stick should still be able to walk slowly.
                return move.sqrMagnitude > 1f ? move.normalized : move;
            }
        }

        public Vector2 Look => new Vector2(
            Input.GetAxis("Mouse X") * _lookSensitivity,
            Input.GetAxis("Mouse Y") * _lookSensitivity * (_invertLookY ? 1f : -1f));

        // Scrolling up should bring the camera closer, so the wheel is inverted here.
        public float Zoom => -Input.GetAxis("Mouse ScrollWheel") * _zoomSensitivity;

        public bool SprintHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
        public bool JumpHeld => Input.GetKey(KeyCode.Space);
        public bool InteractPressed => Input.GetKeyDown(KeyCode.E);
        public bool AttackPressed => Input.GetMouseButtonDown(0);
    }
}
