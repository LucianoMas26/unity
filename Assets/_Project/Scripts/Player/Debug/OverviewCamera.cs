#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// A detached camera for looking at the city from outside, in orthographic projection.
    /// Development only.
    /// <para>
    /// Exists because judging a skyline is impossible from inside it. The third-person camera is
    /// two metres off the ground by design, and Unity's Scene view fights back at city scale: it
    /// frames a six-kilometre object from kilometres away and then flies at walking pace.
    /// </para>
    /// <para>
    /// Works in any scene with a player rig, whatever the world is made of, so the same view can
    /// be used to compare two terrain sources side by side.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class OverviewCamera : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Metres from the pivot. The wheel changes it.")]
        [SerializeField] float _distance = 900f;
        [SerializeField] float _minDistance = 40f;
        [SerializeField] float _maxDistance = 6000f;

        [Tooltip("Degrees above the horizon. 30 is the classic isometric angle.")]
        [SerializeField] float _pitch = 30f;
        [SerializeField] float _yaw = 45f;

        [Tooltip("Orthographic removes perspective entirely, which is what makes relative " +
                 "building heights comparable across the whole view.")]
        [SerializeField] bool _orthographic = true;

        [Header("Movement")]
        [SerializeField] float _panSpeed = 400f;
        [SerializeField] float _orbitSpeed = 4f;

        Camera _camera;
        PlayerCamera _playerCamera;
        PlayerController _controller;
        CameraTuner _tuner;
        Vector3 _pivot;
        float _savedFarClip;
        float _savedNearClip;
        bool _active;
        bool _pivotSet;
        GUIStyle _style;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _playerCamera = GetComponent<PlayerCamera>();
            _tuner = GetComponent<CameraTuner>();
        }

        void Start()
        {
            if (_playerCamera != null && _playerCamera.Target != null)
                _controller = _playerCamera.Target.GetComponent<PlayerController>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) Toggle(!_active);
            if (!_active) return;

            if (Input.GetKeyDown(KeyCode.O))
            {
                _orthographic = !_orthographic;
                _camera.orthographic = _orthographic;
            }

            // Orbit with the left button held, so a stray mouse move does not spin the view.
            if (Input.GetMouseButton(0))
            {
                _yaw += Input.GetAxis("Mouse X") * _orbitSpeed;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * _orbitSpeed, 5f, 89f);
            }

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.0001f)
                _distance = Mathf.Clamp(_distance * Mathf.Exp(-wheel * 3f), _minDistance, _maxDistance);

            // Panning scales with distance: at 3 km you want to cross the city, not shuffle.
            float pan = _panSpeed * (_distance / 900f) * Time.unscaledDeltaTime;
            Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;

            if (Input.GetKey(KeyCode.W)) _pivot += forward * pan;
            if (Input.GetKey(KeyCode.S)) _pivot -= forward * pan;
            if (Input.GetKey(KeyCode.D)) _pivot += right * pan;
            if (Input.GetKey(KeyCode.A)) _pivot -= right * pan;
            if (Input.GetKey(KeyCode.E)) _pivot += Vector3.up * pan;
            if (Input.GetKey(KeyCode.Q)) _pivot -= Vector3.up * pan;

            if (Input.GetKeyDown(KeyCode.R) && _playerCamera != null && _playerCamera.Target != null)
                _pivot = _playerCamera.Target.position;

            Apply();
        }

        void Apply()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _pivot - rotation * Vector3.forward * _distance;
            transform.rotation = rotation;

            // In orthographic the size is what reads as zoom; distance only keeps the near plane clear.
            if (_orthographic) _camera.orthographicSize = _distance * 0.45f;

            // The gameplay far plane is 1.5 km, which clips the whole city away from up here.
            _camera.farClipPlane = _distance * 3f + 3000f;
            _camera.nearClipPlane = Mathf.Max(0.5f, _distance * 0.01f);
        }

        void Toggle(bool active)
        {
            _active = active;

            if (active && !_pivotSet)
            {
                _pivot = _playerCamera != null && _playerCamera.Target != null
                    ? _playerCamera.Target.position
                    : transform.position;
                _pivotSet = true;
            }

            // The player camera and the controller are switched off rather than fought with:
            // two scripts writing the same transform every frame is a fight nobody wins.
            if (_playerCamera != null) _playerCamera.enabled = !active;
            if (_controller != null) _controller.enabled = !active;
            if (_tuner != null) _tuner.enabled = !active;

            _camera.orthographic = active && _orthographic;

            if (active)
            {
                _savedFarClip = _camera.farClipPlane;
                _savedNearClip = _camera.nearClipPlane;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Apply();
            }
            else if (_savedFarClip > 0f)
            {
                _camera.farClipPlane = _savedFarClip;
                _camera.nearClipPlane = _savedNearClip;
            }
        }

        void OnGUI()
        {
            if (!_active)
            {
                var hint = new Rect(12f, Screen.height - 34f, 210f, 24f);
                GUI.Box(hint, GUIContent.none);
                GUI.Label(hint, " F3 = vista desde fuera", Style());
                return;
            }

            var panel = new Rect(12f, 12f, 300f, 186f);
            GUI.Box(panel, GUIContent.none);

            var text = new System.Text.StringBuilder();
            text.AppendLine("VISTA DESDE FUERA");
            text.AppendLine();
            text.AppendLine("  Proyeccion  " + (_orthographic ? "ORTOGRAFICA (isometrica)" : "perspectiva"));
            text.AppendLine("  Distancia   " + _distance.ToString("F0") + " m");
            text.AppendLine("  Angulo      " + _pitch.ToString("F0") + " grados sobre el horizonte");
            text.AppendLine("  Altura      " + _pivot.y.ToString("F0") + " m");
            text.AppendLine();
            text.AppendLine("  Raton izq   orbitar");
            text.AppendLine("  Rueda       acercar / alejar");
            text.AppendLine("  WASD        desplazar    Q/E  bajar/subir");
            text.AppendLine("  O           orto / perspectiva");
            text.AppendLine("  R           volver al personaje");
            text.AppendLine("  F3          volver al juego");

            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 20f, panel.height - 14f),
                      text.ToString(), Style());
        }

        GUIStyle Style()
        {
            if (_style != null) return _style;

            _style = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperLeft };
            _style.normal.textColor = Color.white;
            return _style;
        }
    }
}
#endif
