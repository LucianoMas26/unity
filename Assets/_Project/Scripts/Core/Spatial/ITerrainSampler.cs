using UnityEngine;

namespace Survival.Core
{
    /// <summary>
    /// What non-terrain systems are allowed to ask the world about the ground.
    /// Kept in Core so Player, Creatures and Buildings can all depend on it without depending
    /// on the terrain implementation.
    /// </summary>
    public interface ITerrainSampler
    {
        /// <summary>Ground height in metres at a world XZ position. Always answerable, even
        /// where no chunk has been built yet -- it is a pure function of the seed.</summary>
        float SampleHeight(float worldX, float worldZ);

        /// <summary>True once real collision geometry exists at this position. Physics-driven
        /// objects must wait for this before trusting gravity, or they fall through the world
        /// during the first frames of streaming.</summary>
        bool HasCollisionAt(Vector3 worldPosition);
    }
}
