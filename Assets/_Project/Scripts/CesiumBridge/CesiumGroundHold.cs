#if SURVIVAL_CESIUM
using CesiumForUnity;
using Survival.Player;
using UnityEngine;

namespace Survival.CesiumBridge
{
    /// <summary>
    /// Keeps the player on top of streamed terrain: holds them still until there is real ground,
    /// and puts them back whenever the ground disappears from under them.
    /// <para>
    /// Both halves are necessary, and the second one is the surprise. Cesium refines tiles by
    /// replacing a coarse tile with finer children, so the collider under the player is destroyed
    /// and rebuilt repeatedly during streaming. A character standing on it falls through the gap
    /// and never comes back. Waiting once at startup is not enough.
    /// </para>
    /// <para>
    /// Our own terrain never has this problem: height is a pure function of the seed, so the
    /// surface is known before any mesh exists and the character can be held at exactly the right
    /// altitude. With Cesium the ground is only knowable once it has arrived, and it can leave.
    /// </para>
    /// <para>
    /// A stopgap for the evaluation scene. If Cesium stays, this belongs behind
    /// <c>ITerrainSampler</c> like every other terrain source in the project.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class CesiumGroundHold : MonoBehaviour
    {
        [Tooltip("Tileset whose load progress gates the first placement. Found automatically if empty.")]
        [SerializeField] Cesium3DTileset _tileset;

        [Tooltip("Percent of tiles loaded before the player is placed. Releasing early lands the " +
                 "player on a root tile that is kilometres off and about to be replaced.")]
        [Range(0f, 100f)][SerializeField] float _loadThreshold = 95f;

        [Tooltip("Height above the player to start looking. Must clear the terrain: the " +
                 "georeference can easily sit below the real surface.")]
        [SerializeField] float _searchFromAbove = 3000f;

        [SerializeField] float _searchDistance = 12000f;

        [Tooltip("Metres of clearance left above the ground when the player is placed.")]
        [SerializeField] float _clearance = 0.5f;

        [Tooltip("If ground turns up this far above the player, they fell through a tile that was " +
                 "being refined, and get put back on top.")]
        [SerializeField] float _rescueMargin = 5f;

        [Tooltip("Seconds before giving up on the first placement, so a broken setup does not " +
                 "look like an infinite loading screen.")]
        [SerializeField] float _timeout = 90f;

        CharacterController _controller;
        PlayerController _player;
        float _waited;
        bool _placed;
        int _rescues;

        public bool IsWaiting => !_placed;
        public int RescueCount => _rescues;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _player = GetComponent<PlayerController>();
            if (_tileset == null) _tileset = FindFirstObjectByType<Cesium3DTileset>();

            SetPlayerEnabled(false);   // frozen until there is something to stand on
        }

        void Update()
        {
            if (!_placed) UpdateInitialPlacement();
            else RescueIfBelowGround();
        }

        void UpdateInitialPlacement()
        {
            _waited += Time.deltaTime;

            float progress = _tileset != null ? _tileset.ComputeLoadProgress() : 100f;
            bool timedOut = _waited >= _timeout;

            // Waiting for the tiles to settle is the whole point. The colliders that exist in the
            // first tenth of a second belong to root tiles covering half a continent.
            if (progress < _loadThreshold && !timedOut) return;

            if (!TryFindGround(out float groundY))
            {
                if (!timedOut) return;

                Debug.LogWarning($"[CesiumGroundHold] Sin suelo tras {_timeout:F0} s con la carga al " +
                                 $"{progress:F0} %. Se suelta al jugador igualmente.", this);
                _placed = true;
                SetPlayerEnabled(true);
                return;
            }

            PlaceOn(groundY);
            _placed = true;
            SetPlayerEnabled(true);

            Debug.Log($"[CesiumGroundHold] Colocado a y={groundY:F1} con la carga al {progress:F0} % " +
                      $"tras {_waited:F1} s.", this);
        }

        /// <summary>
        /// Puts the player back whenever refinement pulls the ground out from under them. Without
        /// this a single unlucky frame during streaming drops them through the planet for good.
        /// </summary>
        void RescueIfBelowGround()
        {
            if (!TryFindGround(out float groundY)) return;
            if (groundY <= transform.position.y + _rescueMargin) return;

            PlaceOn(groundY);
            _rescues++;

            if (_rescues <= 5 || _rescues % 25 == 0)
                Debug.LogWarning($"[CesiumGroundHold] El terreno se refino bajo el jugador y cayo; " +
                                 $"devuelto a y={groundY:F1}. Rescate numero {_rescues}.", this);
        }

        void PlaceOn(float groundY)
        {
            Vector3 position = transform.position;
            position.y = groundY + _controller.height * 0.5f + _controller.skinWidth + _clearance;

            _controller.enabled = false;   // moving a CharacterController directly requires this
            transform.position = position;
            _controller.enabled = true;
        }

        /// <summary>
        /// Looks from high above rather than from the player's feet: the terrain is often above
        /// the starting point, where a ray cast downward from the feet would never see it.
        /// </summary>
        bool TryFindGround(out float groundY)
        {
            groundY = 0f;

            Vector3 from = transform.position + Vector3.up * _searchFromAbove;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, _searchDistance,
                                                   ~0, QueryTriggerInteraction.Ignore);

            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.point.y > best) best = hit.point.y;
            }

            if (float.IsNegativeInfinity(best)) return false;

            groundY = best;
            return true;
        }

        void SetPlayerEnabled(bool value)
        {
            if (_player != null) _player.enabled = value;
        }
    }
}
#endif
