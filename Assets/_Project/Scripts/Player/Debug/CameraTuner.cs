#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// Live framing and feel tool. Development only -- the whole file compiles out of a release
    /// build.
    /// <para>
    /// Exists because values changed in the Inspector during Play are discarded on exit, which is
    /// the worst possible property for something that can only be judged by playing. Tune here,
    /// press the copy key, paste the numbers back into the defaults.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerCamera))]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraTuner : MonoBehaviour
    {
        [System.Serializable]
        public struct Preset
        {
            public string Name;
            public float Distance;
            public float Pitch;
            public float FieldOfView;
            public float PivotHeight;
        }

        [Tooltip("Anchors to compare against. Judging a camera in isolation is much harder than " +
                 "flipping between two and saying which one is better.")]
        [SerializeField]
        Preset[] _presets =
        {
            new Preset { Name = "Cerca / agil",      Distance = 3.5f, Pitch = 10f, FieldOfView = 60f, PivotHeight = 1.45f },
            new Preset { Name = "Base",              Distance = 5.0f, Pitch = 15f, FieldOfView = 62f, PivotHeight = 1.50f },
            new Preset { Name = "Lejos / explorar",  Distance = 7.0f, Pitch = 24f, FieldOfView = 66f, PivotHeight = 1.60f },
        };

        [SerializeField] bool _visibleOnStart = true;

        PlayerCamera _camera;
        Camera _lens;
        PlayerController _controller;
        PlayerMovement _movement;
        StaminaSystem _stamina;
        bool _visible;
        float _lastCopiedTime = -10f;
        GUIStyle _style;

        void Awake()
        {
            _camera = GetComponent<PlayerCamera>();
            _lens = GetComponent<Camera>();
            _visible = _visibleOnStart;
        }

        void Start()
        {
            if (_camera.Target == null) return;

            _controller = _camera.Target.GetComponent<PlayerController>();
            _movement = _camera.Target.GetComponent<PlayerMovement>();
            _stamina = _camera.Target.GetComponent<StaminaSystem>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) _visible = !_visible;
            if (Input.GetKeyDown(KeyCode.F2)) CopyValues();

            for (int i = 0; i < _presets.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    Apply(_presets[i]);

            float step = Time.unscaledDeltaTime;

            if (Input.GetKey(KeyCode.PageUp)) _lens.fieldOfView += 20f * step;
            if (Input.GetKey(KeyCode.PageDown)) _lens.fieldOfView -= 20f * step;
            _lens.fieldOfView = Mathf.Clamp(_lens.fieldOfView, 35f, 100f);

            if (Input.GetKey(KeyCode.Home)) _camera.PivotHeight += 1.2f * step;
            if (Input.GetKey(KeyCode.End)) _camera.PivotHeight -= 1.2f * step;
            _camera.PivotHeight = Mathf.Clamp(_camera.PivotHeight, 0f, 4f);

            if (Input.GetKey(KeyCode.Insert)) _camera.PositionSmoothing += 0.12f * step;
            if (Input.GetKey(KeyCode.Delete)) _camera.PositionSmoothing -= 0.12f * step;
            _camera.PositionSmoothing = Mathf.Clamp(_camera.PositionSmoothing, 0f, 0.4f);

            if (Input.GetKeyDown(KeyCode.R) && _stamina != null) _stamina.Refill();
        }

        void Apply(Preset preset)
        {
            _camera.Distance = preset.Distance;
            _camera.Pitch = preset.Pitch;
            _camera.PivotHeight = preset.PivotHeight;
            _lens.fieldOfView = preset.FieldOfView;
        }

        void CopyValues()
        {
            var text = new StringBuilder();
            text.AppendLine("PlayerCamera");
            text.AppendLine("  Distance            = " + _camera.Distance.ToString("F2") + "f");
            text.AppendLine("  Start Pitch         = " + _camera.Pitch.ToString("F1") + "f");
            text.AppendLine("  Pivot Height        = " + _camera.PivotHeight.ToString("F2") + "f");
            text.AppendLine("  Position Smoothing  = " + _camera.PositionSmoothing.ToString("F3") + "f");
            text.AppendLine("Camera");
            text.AppendLine("  Field of View       = " + _lens.fieldOfView.ToString("F1"));
            text.AppendLine("Derived");
            text.AppendLine("  Altura sobre pies   = " + _camera.HeightAboveFeet.ToString("F2") + " m");

            _lastCopiedTime = Time.unscaledTime;
            GUIUtility.systemCopyBuffer = text.ToString();
            Debug.Log("[CameraTuner] Valores copiados al portapapeles:\n" + text);
        }

        void OnGUI()
        {
            if (!_visible)
            {
                var hint = new Rect(12f, 12f, 200f, 24f);
                GUI.Box(hint, GUIContent.none);
                GUI.Label(hint, " F1 = panel de ajuste", Style());
                return;
            }

            var text = new StringBuilder();

            text.AppendLine("CAMARA");
            text.AppendLine("  Distancia        " + _camera.Distance.ToString("F2") + " m");
            text.AppendLine("  Pitch            " + _camera.Pitch.ToString("F1") + " grados");
            text.AppendLine("  FOV              " + _lens.fieldOfView.ToString("F1"));
            text.AppendLine("  Altura pivote    " + _camera.PivotHeight.ToString("F2") + " m");
            text.AppendLine("  Altura sobre pies " + _camera.HeightAboveFeet.ToString("F2") + " m");
            text.AppendLine("  Suavizado pos.   " + _camera.PositionSmoothing.ToString("F3") + " s");

            if (_movement != null)
            {
                text.AppendLine();
                text.AppendLine("MOVIMIENTO");
                text.AppendLine("  Velocidad        " + _movement.CurrentSpeed.ToString("F1") + " m/s"
                                + (_movement.IsSprinting ? "  (sprint)" : ""));
                text.AppendLine("  En suelo         " + (_movement.IsGrounded ? "si" : "no"));
                text.AppendLine("  Vel. vertical    " + _movement.VerticalVelocity.ToString("F1") + " m/s");
            }

            if (_stamina != null)
            {
                text.AppendLine();
                text.AppendLine("STAMINA");
                text.AppendLine("  " + Bar(_stamina.Normalised) + "  " + _stamina.Current.ToString("F0")
                                + " / " + _stamina.Maximum.ToString("F0")
                                + (_stamina.IsExhausted ? "   AGOTADO" : ""));
            }

            text.AppendLine();
            text.AppendLine("JUEGO");
            text.AppendLine("  Raton      girar la camara");
            text.AppendLine("  WASD       mover (relativo a camara)");
            text.AppendLine("  Shift      sprint     Espacio  saltar");
            text.AppendLine("  Rueda      zoom       Esc      soltar cursor");

            text.AppendLine();
            text.AppendLine("AJUSTE");
            for (int i = 0; i < _presets.Length && i < 9; i++)
                text.AppendLine("  " + (i + 1) + "  " + _presets[i].Name);
            text.AppendLine("  PgUp/PgDn  FOV");
            text.AppendLine("  Home/End   altura del pivote");
            text.AppendLine("  Ins/Supr   suavizado de posicion");
            text.AppendLine("  R          rellenar stamina");
            text.AppendLine("  F2         copiar valores");
            text.AppendLine("  F1         ocultar");

            if (Time.unscaledTime - _lastCopiedTime < 2.5f)
            {
                text.AppendLine();
                text.AppendLine(">> copiado al portapapeles");
            }

            // A box behind the text, because white-on-sky is unreadable exactly where the
            // horizon is, which is where the panel always sits.
            var panel = new Rect(12f, 12f, 330f, 590f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 16f, panel.height - 12f),
                      text.ToString(), Style());
        }

        static string Bar(float normalised)
        {
            const int Width = 20;
            int filled = Mathf.RoundToInt(Mathf.Clamp01(normalised) * Width);
            return "[" + new string('=', filled) + new string('.', Width - filled) + "]";
        }

        GUIStyle Style()
        {
            if (_style != null) return _style;

            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };
            _style.normal.textColor = Color.white;
            return _style;
        }
    }
}
#endif
