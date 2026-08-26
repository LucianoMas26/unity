using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// Keeps the camera out of walls. Sphere-casts from the pivot towards where the camera wants
    /// to sit and reports how far it is actually allowed to go.
    /// <para>
    /// Its own component because the rule it enforces is asymmetric and worth tuning separately:
    /// pulling in has to be instant, or the camera is already inside the wall by the time it
    /// reacts; easing back out has to be gradual, or brushing past a pillar snaps the whole view.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraCollision : MonoBehaviour
    {
        [Tooltip("Layers the camera will not clip through. Terrain and buildings belong here.")]
        [SerializeField] LayerMask _obstacleMask = ~0;

        [Tooltip("Radius of the probe. Roughly the camera's near-plane, so corners do not clip.")]
        [SerializeField] float _probeRadius = 0.28f;

        [Tooltip("Extra gap kept between the camera and whatever it pulled in against.")]
        [SerializeField] float _padding = 0.15f;

        [Tooltip("Never closer than this, however tight the space.")]
        [SerializeField] float _minimumDistance = 0.6f;

        [Tooltip("How quickly the camera eases back out once the obstacle is gone, in metres per " +
                 "second. Pulling in is always instant.")]
        [SerializeField] float _returnSpeed = 6f;

        float _currentDistance = -1f;

        public bool IsObstructed { get; private set; }

        /// <summary>
        /// The distance the camera may actually use this frame.
        /// </summary>
        /// <param name="pivot">Point the camera orbits, in world space.</param>
        /// <param name="directionToCamera">Unit vector from the pivot towards the camera.</param>
        /// <param name="desiredDistance">Distance the camera would like, ignoring obstacles.</param>
        public float Resolve(Vector3 pivot, Vector3 directionToCamera, float desiredDistance, float deltaTime)
        {
            float allowed = desiredDistance;
            IsObstructed = false;

            if (Physics.SphereCast(pivot, _probeRadius, directionToCamera, out RaycastHit hit,
                                   desiredDistance, _obstacleMask, QueryTriggerInteraction.Ignore))
            {
                allowed = Mathf.Max(_minimumDistance, hit.distance - _padding);
                IsObstructed = true;
            }

            if (_currentDistance < 0f) _currentDistance = allowed; // first frame

            _currentDistance = allowed < _currentDistance
                ? allowed                                                      // instant pull in
                : Mathf.MoveTowards(_currentDistance, allowed, _returnSpeed * deltaTime);

            return _currentDistance;
        }

        /// <summary>Forgets the eased distance, so the next resolve snaps. Use after teleports.</summary>
        public void Reset() => _currentDistance = -1f;
    }
}
