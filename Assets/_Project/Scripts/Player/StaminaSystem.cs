using System;
using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// A depleting, regenerating resource. Sprint spends it today; climbing, gliding and heavy
    /// attacks are the obvious next customers, which is why nothing in here mentions sprinting.
    /// <para>
    /// Owns no update loop of its own. Whoever spends the stamina calls <see cref="Tick"/> once
    /// per frame, so drain and regeneration cannot disagree about what happened this frame.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StaminaSystem : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField] float _maximum = 100f;

        [Header("Rates")]
        [Tooltip("Units drained per second while spending. At 100 max and 20/s, a sprint lasts 5 seconds.")]
        [SerializeField] float _drainPerSecond = 20f;

        [SerializeField] float _regenPerSecond = 28f;

        [Tooltip("Seconds of not spending before regeneration starts. Without a pause here, " +
                 "tapping sprint on and off is strictly better than holding it.")]
        [SerializeField] float _regenDelay = 0.7f;

        [Header("Exhaustion")]
        [Tooltip("After hitting zero, this much has to regenerate before spending is allowed " +
                 "again. Stops the stutter of sprinting for one frame per frame at empty.")]
        [SerializeField] float _recoveryThreshold = 20f;

        float _current;
        float _timeSinceSpend;
        bool _exhausted;

        /// <summary>Raised when the pool empties, and again when it recovers. The HUD will want
        /// this, and so will anything that flashes or grunts when the player runs out.</summary>
        public event Action<bool> ExhaustedChanged;

        public float Current => _current;
        public float Maximum => _maximum;
        public float Normalised => _maximum > 0f ? _current / _maximum : 0f;

        /// <summary>True while the pool is locked out after emptying.</summary>
        public bool IsExhausted => _exhausted;

        /// <summary>Whether a spender is allowed to spend right now.</summary>
        public bool CanSpend => !_exhausted && _current > 0f;

        void Awake() => _current = _maximum;

        /// <summary>
        /// Advances the pool by one frame. Pass whether something is actually spending: drain
        /// and regeneration are mutually exclusive, and letting each system decide separately is
        /// how you end up regenerating mid-sprint.
        /// </summary>
        public void Tick(float deltaTime, bool spending)
        {
            if (spending && CanSpend)
            {
                _current -= _drainPerSecond * deltaTime;
                _timeSinceSpend = 0f;
            }
            else
            {
                _timeSinceSpend += deltaTime;
                if (_timeSinceSpend >= _regenDelay)
                    _current += _regenPerSecond * deltaTime;
            }

            _current = Mathf.Clamp(_current, 0f, _maximum);
            UpdateExhaustion();
        }

        void UpdateExhaustion()
        {
            bool wasExhausted = _exhausted;

            if (_exhausted)
            {
                if (_current >= _recoveryThreshold) _exhausted = false;
            }
            else if (_current <= 0f)
            {
                _exhausted = true;
            }

            if (_exhausted != wasExhausted) ExhaustedChanged?.Invoke(_exhausted);
        }

        /// <summary>One-off cost, for things that are not spent over time. Returns false and
        /// spends nothing if the pool cannot cover it.</summary>
        public bool TrySpend(float amount)
        {
            if (_exhausted || _current < amount) return false;

            _current -= amount;
            _timeSinceSpend = 0f;
            UpdateExhaustion();
            return true;
        }

        public void Refill() => _current = _maximum;

        void OnValidate()
        {
            _maximum = Mathf.Max(1f, _maximum);
            _recoveryThreshold = Mathf.Clamp(_recoveryThreshold, 0f, _maximum);
            _regenDelay = Mathf.Max(0f, _regenDelay);
        }
    }
}
