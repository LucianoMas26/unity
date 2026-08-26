using UnityEngine;

namespace Survival.Core
{
    /// <summary>
    /// Hash-based 2D gradient (Perlin-style) noise. Every value is derived from
    /// <see cref="DeterministicHash"/>, so it is identical on every platform and Unity
    /// version -- unlike Mathf.PerlinNoise, whose implementation is not contractual.
    /// </summary>
    public static class GradientNoise
    {
        // 8 unit-ish gradients. Keeps the dot products cheap and the field isotropic enough.
        static readonly float[] GradX = { 1f, -1f, 1f, -1f, 0.7071f, -0.7071f, 0.7071f, -0.7071f };
        static readonly float[] GradY = { 0f, 0f, 0f, 0f, 0.7071f, 0.7071f, -0.7071f, -0.7071f };

        const float Normalisation = 1.4142f; // raw 2D gradient noise peaks near 1/sqrt(2)

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static float Dot(uint seed, int ix, int iy, float dx, float dy)
        {
            int g = (int)(DeterministicHash.Hash(seed, ix, iy) & 7u);
            return GradX[g] * dx + GradY[g] * dy;
        }

        /// <summary>Single octave, output roughly in [-1, 1].</summary>
        public static float Sample(uint seed, float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;

            float u = Fade(fx);
            float v = Fade(fy);

            float n00 = Dot(seed, ix, iy, fx, fy);
            float n10 = Dot(seed, ix + 1, iy, fx - 1f, fy);
            float n01 = Dot(seed, ix, iy + 1, fx, fy - 1f);
            float n11 = Dot(seed, ix + 1, iy + 1, fx - 1f, fy - 1f);

            float a = Mathf.Lerp(n00, n10, u);
            float b = Mathf.Lerp(n01, n11, u);
            return Mathf.Clamp(Mathf.Lerp(a, b, v) * Normalisation, -1f, 1f);
        }

        /// <summary>Fractal Brownian motion. Output normalised to roughly [-1, 1].</summary>
        public static float Fbm(uint seed, float x, float y, in FractalNoiseSettings settings)
        {
            float amplitude = 1f;
            float frequency = settings.Frequency;
            float sum = 0f;
            float normaliser = 0f;

            int octaves = Mathf.Max(1, settings.Octaves);
            for (int i = 0; i < octaves; i++)
            {
                // Offsetting the seed per octave keeps octaves from lining up on the same lattice.
                uint octaveSeed = DeterministicHash.Salt(seed, (uint)(i + 1));
                float value = Sample(octaveSeed, x * frequency, y * frequency);

                if (settings.Ridged)
                    value = 1f - Mathf.Abs(value) * 2f; // -1 in valleys, +1 along sharp ridges

                sum += value * amplitude;
                normaliser += amplitude;

                amplitude *= settings.Gain;
                frequency *= settings.Lacunarity;
            }

            return normaliser > 0f ? sum / normaliser : 0f;
        }

        /// <summary>Fbm remapped to [0, 1]. Convenient for masks and density fields.</summary>
        public static float Fbm01(uint seed, float x, float y, in FractalNoiseSettings settings)
            => Fbm(seed, x, y, settings) * 0.5f + 0.5f;
    }
}
