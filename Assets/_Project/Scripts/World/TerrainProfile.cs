using System;
using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// Pure data describing how one region shapes its ground. Deliberately a plain struct with
    /// no Unity object references: chunk meshes are built on worker threads, and touching a
    /// ScriptableObject from one is not safe. Regions hand out a copy of this instead.
    /// </summary>
    [Serializable]
    public struct TerrainProfile
    {
        [Header("Base shape")]
        [Tooltip("Metres above sea level the region sits at before any noise is applied.")]
        public float BaseElevation;

        [Tooltip("How many metres the broad rolling shape adds or removes.")]
        public float ContinentAmplitude;
        public FractalNoiseSettings ContinentNoise;

        [Header("Mountains")]
        [Tooltip("Peak height added where the mountain mask is fully open.")]
        public float MountainAmplitude;
        [Tooltip("Ridged noise. This is what makes sharp crests instead of soft blobs.")]
        public FractalNoiseSettings MountainNoise;
        [Tooltip("Decides WHERE mountains are allowed at all, so ranges cluster.")]
        public FractalNoiseSettings MountainMaskNoise;
        [Range(0f, 1f)] public float MountainMaskThreshold;

        [Header("Detail")]
        public float DetailAmplitude;
        public FractalNoiseSettings DetailNoise;

        [Header("Water")]
        public float WaterLevel;

        public static TerrainProfile Default => new TerrainProfile
        {
            BaseElevation = 30f,
            ContinentAmplitude = 70f,
            ContinentNoise = FractalNoiseSettings.Create(0.00035f, 4),
            MountainAmplitude = 190f,
            MountainNoise = FractalNoiseSettings.Create(0.0016f, 5, ridged: true),
            MountainMaskNoise = FractalNoiseSettings.Create(0.00022f, 3),
            MountainMaskThreshold = 0.52f,
            DetailAmplitude = 5f,
            DetailNoise = FractalNoiseSettings.Create(0.02f, 3),
            WaterLevel = 8f
        };
    }

    /// <summary>Placeholder shading, so terrain reads clearly before any art exists.</summary>
    [Serializable]
    public struct TerrainPalette
    {
        public Color Lowland;
        public Color Highland;
        public Color Rock;
        public Color Peak;
        public Color Shore;

        [Tooltip("Slope steepness (0 = flat, 1 = vertical) at which ground becomes bare rock.")]
        [Range(0f, 1f)] public float RockSlope;

        [Tooltip("Height in metres where highland colouring starts blending in.")]
        public float HighlandHeight;
        public float PeakHeight;

        public static TerrainPalette Default => new TerrainPalette
        {
            // Muted, slightly sickly greens and greys: stylised and grim, not photoreal.
            Lowland = new Color(0.32f, 0.38f, 0.24f),
            Highland = new Color(0.27f, 0.30f, 0.22f),
            Rock = new Color(0.30f, 0.29f, 0.29f),
            Peak = new Color(0.62f, 0.63f, 0.61f),
            Shore = new Color(0.44f, 0.42f, 0.33f),
            RockSlope = 0.55f,
            HighlandHeight = 90f,
            PeakHeight = 210f
        };
    }
}
