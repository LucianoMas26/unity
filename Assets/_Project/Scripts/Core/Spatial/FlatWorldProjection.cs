using System;
using UnityEngine;

namespace Survival.Core
{
    /// <summary>
    /// East-North-Up tangent plane anchored at an origin lat/lon on the WGS84 ellipsoid.
    /// X = metres east, Z = metres north, Y = metres up. Error is negligible across a few tens
    /// of kilometres and grows with distance -- correct for the prototype region, and the
    /// obvious thing to replace once the world stops being flat.
    /// </summary>
    public sealed class FlatWorldProjection : IWorldProjection
    {
        const double SemiMajorAxis = 6378137.0;        // WGS84 a
        const double Flattening = 1.0 / 298.257223563; // WGS84 f
        const double DegToRad = Math.PI / 180.0;
        static readonly double EccentricitySquared = Flattening * (2.0 - Flattening);

        readonly double _metersPerDegLat;
        readonly double _metersPerDegLon;

        public GeoCoordinate Origin { get; }

        public FlatWorldProjection(GeoCoordinate origin)
        {
            Origin = origin;
            _metersPerDegLat = MetersPerDegreeLatitude(origin.LatitudeDeg);
            _metersPerDegLon = MetersPerDegreeLongitude(origin.LatitudeDeg);
        }

        public GeoCoordinate WorldToGeo(Vector3 worldPosition) => new GeoCoordinate(
            Origin.LatitudeDeg + worldPosition.z / _metersPerDegLat,
            Origin.LongitudeDeg + worldPosition.x / _metersPerDegLon,
            Origin.ElevationMeters + worldPosition.y);

        public Vector3 GeoToWorld(GeoCoordinate geo) => new Vector3(
            (float)((geo.LongitudeDeg - Origin.LongitudeDeg) * _metersPerDegLon),
            (float)(geo.ElevationMeters - Origin.ElevationMeters),
            (float)((geo.LatitudeDeg - Origin.LatitudeDeg) * _metersPerDegLat));

        /// <summary>Meridional radius of curvature, expressed as metres per degree.</summary>
        public double MetersPerDegreeLatitude(double latitudeDeg)
        {
            double sinLat = Math.Sin(latitudeDeg * DegToRad);
            double denominator = 1.0 - EccentricitySquared * sinLat * sinLat;
            double m = SemiMajorAxis * (1.0 - EccentricitySquared) / Math.Pow(denominator, 1.5);
            return m * DegToRad;
        }

        /// <summary>Prime vertical radius of curvature scaled by cos(lat).</summary>
        public double MetersPerDegreeLongitude(double latitudeDeg)
        {
            double latRad = latitudeDeg * DegToRad;
            double sinLat = Math.Sin(latRad);
            double n = SemiMajorAxis / Math.Sqrt(1.0 - EccentricitySquared * sinLat * sinLat);
            return n * Math.Cos(latRad) * DegToRad;
        }
    }
}
