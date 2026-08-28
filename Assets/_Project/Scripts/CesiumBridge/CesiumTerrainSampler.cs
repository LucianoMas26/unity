#if SURVIVAL_CESIUM
using CesiumForUnity;
using Survival.Core;
using UnityEngine;

namespace Survival.CesiumBridge
{
    /// <summary>
    /// Lets the rest of the project ask Cesium where the ground is, through the same
    /// <see cref="ITerrainSampler"/> everything already uses.
    /// <para>
    /// This is the seam paying off. Buildings, the player and anything else that needs to stand
    /// on something were written against an interface, not against our own terrain, so pointing
    /// them at Cesium is a matter of registering a different implementation.
    /// </para>
    /// <para>
    /// The honest difference is that this one has to raycast. Our own terrain answers from the
    /// seed before any mesh exists; Cesium can only answer once a tile has arrived, and it stops
    /// answering while a tile is being refined. Callers must check
    /// <see cref="HasCollisionAt"/> rather than trusting a height.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CesiumTerrainSampler : MonoBehaviour, ITerrainSampler
    {
        [SerializeField] Cesium3DTileset _tileset;

        [Tooltip("Height above the query point to start the ray. Must clear the terrain: the " +
                 "georeference is measured from the ellipsoid and can sit below the surface.")]
        [SerializeField] float _probeFrom = 3000f;

        [SerializeField] float _probeDistance = 12000f;

        [Tooltip("Percent of tiles loaded before answers are considered trustworthy.")]
        [Range(0f, 100f)][SerializeField] float _readyThreshold = 90f;

        [SerializeField] LayerMask _groundMask = ~0;

        float _lastHeight;

        /// <summary>Whether enough of the tileset has arrived to trust what this reports.</summary>
        public bool IsReady => _tileset == null || _tileset.ComputeLoadProgress() >= _readyThreshold;

        void Awake()
        {
            if (_tileset == null) _tileset = FindFirstObjectByType<Cesium3DTileset>();
        }

        void OnEnable() => ServiceRegistry.Register<ITerrainSampler>(this);

        void OnDisable() => ServiceRegistry.Unregister<ITerrainSampler>(this);

        /// <summary>
        /// Highest surface at this position, or the last successful answer when no tile is
        /// loaded there. Returning the last height rather than zero matters: zero is a real
        /// altitude, and something placed at it would be buried or floating.
        /// </summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            var from = new Vector3(worldX, _probeFrom, worldZ);

            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, _probeDistance,
                                _groundMask, QueryTriggerInteraction.Ignore))
            {
                _lastHeight = hit.point.y;
            }

            return _lastHeight;
        }

        public bool HasCollisionAt(Vector3 worldPosition)
        {
            var from = new Vector3(worldPosition.x, _probeFrom, worldPosition.z);
            return Physics.Raycast(from, Vector3.down, _probeDistance, _groundMask,
                                   QueryTriggerInteraction.Ignore);
        }

        /// <summary>Ground height, and whether it was actually found rather than remembered.</summary>
        public bool TrySampleHeight(float worldX, float worldZ, out float height)
        {
            var from = new Vector3(worldX, _probeFrom, worldZ);

            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, _probeDistance,
                                _groundMask, QueryTriggerInteraction.Ignore))
            {
                height = _lastHeight = hit.point.y;
                return true;
            }

            height = _lastHeight;
            return false;
        }
    }
}
#endif
