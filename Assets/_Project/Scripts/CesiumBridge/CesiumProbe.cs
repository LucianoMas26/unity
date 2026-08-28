#if SURVIVAL_CESIUM
using CesiumForUnity;
using UnityEngine;

namespace Survival.CesiumBridge
{
    /// <summary>
    /// Answers, on screen and in the log, the four questions that decide whether Cesium stays.
    /// <para>
    /// This exists because the documentation does not answer them. Whether tiles carry colliders,
    /// whether origin shifting survives a CharacterController, and whether it all works on this
    /// Unity version are things you find out by running it, not by reading about it.
    /// </para>
    /// <para>
    /// Development only. It is a measuring instrument, not a game system.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CesiumProbe : MonoBehaviour
    {
        [SerializeField] Cesium3DTileset _tileset;
        [SerializeField] Transform _player;
        [SerializeField] CesiumGeoreference _georeference;

        [Tooltip("How far down to look for ground under the player. Tiles stream in, so early " +
                 "frames legitimately have nothing there.")]
        [SerializeField] float _groundProbeDistance = 500f;

        float _loadProgress;
        bool _groundFound;
        string _groundCollider = "-";
        float _groundDistance;
        float _peakOriginDistance;
        float _nextLog;
        GUIStyle _style;

        void Awake()
        {
            if (_tileset == null) _tileset = FindFirstObjectByType<Cesium3DTileset>();
            if (_georeference == null) _georeference = FindFirstObjectByType<CesiumGeoreference>();
        }

        void Update()
        {
            if (_tileset != null) _loadProgress = _tileset.ComputeLoadProgress();

            if (_player != null)
            {
                // Question 2: do the streamed tiles actually carry collision? A CharacterController
                // has nothing to stand on without it, whatever the tiles look like.
                // The player's own capsule sits right under the ray origin, so a plain Raycast
                // reports the player as ground and the check silently passes for the wrong reason.
                Vector3 from = _player.position + Vector3.up * 2f;
                RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, _groundProbeDistance,
                                                       ~0, QueryTriggerInteraction.Ignore);

                _groundFound = false;
                _groundDistance = 0f;
                _groundCollider = "-";

                float nearest = float.MaxValue;
                foreach (RaycastHit candidate in hits)
                {
                    if (candidate.collider.transform.IsChildOf(_player)) continue;
                    if (candidate.distance >= nearest) continue;

                    nearest = candidate.distance;
                    _groundFound = true;
                    _groundDistance = candidate.distance;
                    _groundCollider = candidate.collider.name;
                }

                // Question 3: origin shifting is meant to keep the player near Unity's origin.
                // If this number grows without bound, float precision will fail eventually.
                float originDistance = _player.position.magnitude;
                if (originDistance > _peakOriginDistance) _peakOriginDistance = originDistance;
            }

            if (Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 10f;
                Debug.Log($"[CesiumProbe] carga {_loadProgress:F0}% | suelo {(_groundFound ? "SI" : "NO")} " +
                          $"({_groundCollider}) | distancia al origen {(_player != null ? _player.position.magnitude : 0f):F0} m " +
                          $"(pico {_peakOriginDistance:F0}) | {(int)(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime))} fps");
            }
        }

        void OnGUI()
        {
            var panel = new Rect(12f, 12f, 330f, 232f);
            GUI.Box(panel, GUIContent.none);

            var text = new System.Text.StringBuilder();
            text.AppendLine("EVALUACION DE CESIUM");
            text.AppendLine();
            text.AppendLine("1. Carga de tiles");
            text.AppendLine("   " + (_tileset == null ? "sin tileset en la escena"
                                                      : _loadProgress.ToString("F0") + " %"));
            text.AppendLine();
            text.AppendLine("2. Colisiones en los tiles");
            text.AppendLine("   " + (_groundFound
                ? "SI  " + _groundCollider + "  a " + _groundDistance.ToString("F1") + " m"
                : "todavia no  (o createPhysicsMeshes off)"));
            text.AppendLine();
            text.AppendLine("3. Origin shift");
            if (_player != null)
            {
                text.AppendLine("   distancia al origen " + _player.position.magnitude.ToString("F0") + " m");
                text.AppendLine("   pico " + _peakOriginDistance.ToString("F0") + " m");
                var anchor = _player.GetComponent<CesiumGlobeAnchor>();
                if (anchor != null)
                {
                    // x is longitude, y is latitude, z is height. The separate latitude and
                    // longitude properties are deprecated in this version.
                    Unity.Mathematics.double3 lonLatHeight = anchor.longitudeLatitudeHeight;
                    text.AppendLine("   lat " + lonLatHeight.y.ToString("F5") +
                                    "  lon " + lonLatHeight.x.ToString("F5"));
                }
            }
            else
            {
                text.AppendLine("   sin jugador asignado");
            }

            text.AppendLine();
            text.AppendLine("4. Cuota: se mira en ion.cesium.com,");
            text.AppendLine("   no es medible desde aqui.");
            text.AppendLine();
            text.AppendLine((int)(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)) + " fps");

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
