using UnityEngine;

namespace Survival.Core
{
    /// <summary>
    /// Maps between Unity world space and real geographic coordinates.
    /// <para>
    /// The prototype uses <see cref="FlatWorldProjection"/>: a local tangent plane, accurate to
    /// centimetres over the 5x5 km test region and free to compute. A planet-scale build would
    /// swap in an ECEF / floating-origin implementation without any generator needing to change
    /// -- which is the whole reason this interface exists this early.
    /// </para>
    /// </summary>
    public interface IWorldProjection
    {
        GeoCoordinate Origin { get; }
        GeoCoordinate WorldToGeo(Vector3 worldPosition);
        Vector3 GeoToWorld(GeoCoordinate geo);
        double MetersPerDegreeLatitude(double latitudeDeg);
        double MetersPerDegreeLongitude(double latitudeDeg);
    }
}
