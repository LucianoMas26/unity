using Survival.Core;

namespace Survival.World
{
    /// <summary>
    /// Answers "which region is this position in?". The prototype answers the same thing
    /// everywhere; a world built from real geography will answer from latitude, climate bands
    /// and land cover data. Every generator downstream is written against this, not against
    /// the single-region case.
    /// </summary>
    public interface IRegionProvider
    {
        RegionSnapshot GetRegion(float worldX, float worldZ);
    }

    /// <summary>The prototype implementation: one region, the whole 5x5 km test patch.</summary>
    public sealed class SingleRegionProvider : IRegionProvider
    {
        readonly RegionSnapshot _region;

        public SingleRegionProvider(RegionSnapshot region) => _region = region ?? RegionSnapshot.CreateFallback();

        public RegionSnapshot GetRegion(float worldX, float worldZ) => _region;
    }

    // TODO(long-term): GeoRegionProvider -- resolves the region from GeoCoordinate using real
    // climate/landcover bands. Same interface, so nothing downstream changes.
    // It will need the IWorldProjection from WorldContext to turn world XZ into lat/lon.
}
