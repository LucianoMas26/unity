using System;
using UnityEngine;

namespace Survival.GeoData
{
    /// <summary>
    /// What a real building is treated as. OSM describes the world, not a game, so every tag has
    /// to be collapsed onto something the generators can act on.
    /// <para>
    /// Worth knowing before relying on this: in the Rosario extract 90% of buildings are tagged
    /// only <c>building=yes</c> and land on <see cref="Unknown"/>. The specific archetypes are a
    /// few hundred landmarks, not the bulk of the city.
    /// </para>
    /// </summary>
    public enum BuildingArchetype
    {
        Unknown = 0,
        House = 1,
        Apartments = 2,
        Commercial = 3,
        Retail = 4,
        Industrial = 5,
        Hospital = 6,
        School = 7,
        Religious = 8,
        Civic = 9,
        Roof = 10,
        Parking = 11,
    }

    public enum RoadClass
    {
        Other = 0,
        Major = 1,
        Secondary = 2,
        Local = 3,
        Path = 4,
    }

    /// <summary>
    /// One real building. The oriented box is what the placeholder pass draws; the footprint
    /// polygon is the real data, kept for the modular building system that will replace it.
    /// </summary>
    [Serializable]
    public struct GeoBuilding
    {
        public Vector2 Centre;
        public Vector2 Size;
        public float RotationDegrees;
        public float Height;
        public BuildingArchetype Archetype;

        /// <summary>Range into the dataset's shared point pool.</summary>
        public int PointStart;
        public int PointCount;
    }

    [Serializable]
    public struct GeoWay
    {
        public int PointStart;
        public int PointCount;
        public RoadClass Class;
        public float Width;
    }

    [Serializable]
    public struct GeoArea
    {
        public int PointStart;
        public int PointCount;
    }
}
