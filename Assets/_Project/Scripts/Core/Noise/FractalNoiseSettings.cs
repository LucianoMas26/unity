using System;
using UnityEngine;

namespace Survival.Core
{
    /// <summary>Inspector-friendly parameters for one fractal noise field.</summary>
    [Serializable]
    public struct FractalNoiseSettings
    {
        [Tooltip("Cycles per world unit. 0.001 = features roughly 1 km across.")]
        public float Frequency;

        [Range(1, 10)] public int Octaves;

        [Tooltip("Frequency multiplier per octave. ~2 is standard.")]
        public float Lacunarity;

        [Tooltip("Amplitude multiplier per octave. <1 keeps detail subtle.")]
        [Range(0.05f, 1f)] public float Gain;

        [Tooltip("Folds the noise around zero to produce sharp ridges (mountains, dunes).")]
        public bool Ridged;

        public static FractalNoiseSettings Default => new FractalNoiseSettings
        {
            Frequency = 0.002f,
            Octaves = 4,
            Lacunarity = 2f,
            Gain = 0.5f,
            Ridged = false
        };

        public static FractalNoiseSettings Create(float frequency, int octaves, bool ridged = false)
            => new FractalNoiseSettings
            {
                Frequency = frequency,
                Octaves = octaves,
                Lacunarity = 2f,
                Gain = 0.5f,
                Ridged = ridged
            };
    }
}
