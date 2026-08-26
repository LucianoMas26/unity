using System;

namespace Survival.Core
{
    /// <summary>
    /// A real-world position. The prototype never leaves a fictional 5x5 km patch, but every
    /// world position can already be expressed as one of these -- that is the seam where real
    /// elevation (SRTM) and OpenStreetMap data will eventually plug in.
    /// </summary>
    [Serializable]
    public struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        public double LatitudeDeg;
        public double LongitudeDeg;
        public double ElevationMeters;

        public GeoCoordinate(double latitudeDeg, double longitudeDeg, double elevationMeters = 0d)
        {
            LatitudeDeg = latitudeDeg;
            LongitudeDeg = longitudeDeg;
            ElevationMeters = elevationMeters;
        }

        /// <summary>
        /// Stable integer key for this coordinate at a given precision, so a world cell can
        /// derive its seed from where it sits on Earth rather than from an arbitrary index.
        /// The long-term plan hinges on this: swap the seed source, keep every generator.
        /// </summary>
        public uint ToSeed(uint worldSeed, double degreesPerCell = 0.001d)
        {
            int latCell = (int)Math.Floor(LatitudeDeg / degreesPerCell);
            int lonCell = (int)Math.Floor(LongitudeDeg / degreesPerCell);
            return DeterministicHash.Hash(worldSeed, latCell, lonCell);
        }

        public bool Equals(GeoCoordinate other)
            => LatitudeDeg.Equals(other.LatitudeDeg)
               && LongitudeDeg.Equals(other.LongitudeDeg)
               && ElevationMeters.Equals(other.ElevationMeters);

        public override bool Equals(object obj) => obj is GeoCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(LatitudeDeg, LongitudeDeg, ElevationMeters);
        public override string ToString() => $"{LatitudeDeg:F6}, {LongitudeDeg:F6} @ {ElevationMeters:F1}m";
    }
}
