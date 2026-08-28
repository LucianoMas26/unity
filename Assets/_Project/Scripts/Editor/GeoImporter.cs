using System;
using Survival.GeoData;
using UnityEditor;
using UnityEngine;

namespace Survival.EditorTools
{
    /// <summary>
    /// Turns downloaded elevation and OpenStreetMap JSON into a <see cref="GeoDataset"/> asset.
    /// <para>
    /// Import is separate from download on purpose. The download is slow, rate-limited and can
    /// fail halfway; the import is instant and repeatable. Keeping the raw JSON in the project
    /// means the parsing can be fixed and re-run without asking two public APIs for the same
    /// five square kilometres again.
    /// </para>
    /// </summary>
    public static class GeoImporter
    {
        const string SourceFolder = ProjectPaths.Root + "/GeoData/Source";
        const string DatasetFolder = ProjectPaths.Root + "/GeoData";

        // JSON shapes. Field names match the files exactly: JsonUtility does not rename.
        [Serializable] class ElevationJson
        {
            public double latMin, latMax, lonMin, lonMax;
            public int resolution;
            public float[] elevations;
        }

        [Serializable] class BuildingJson { public float cx, cy, sx, sy, rot, h; public int arch, ps, pc; }
        [Serializable] class RoadJson { public int ps, pc, cls; public float w; }
        [Serializable] class AreaJson { public int ps, pc; }

        [Serializable] class OsmJson
        {
            public double originLat, originLon;
            public float sizeX, sizeZ;
            public BuildingJson[] buildings;
            public RoadJson[] roads;
            public AreaJson[] water;
            public AreaJson[] parks;
            public float[] px, py;
        }

        [MenuItem("Survival/Geo/Import Rosario Dataset", false, 40)]
        public static void ImportRosario() => Import("Rosario");

        /// <summary>
        /// Builds (or rebuilds) a dataset asset from <c>{name}_elevation.json</c> and
        /// <c>{name}_osm.json</c>. Nothing here is Rosario-specific beyond the file name.
        /// </summary>
        public static GeoDataset Import(string regionName)
        {
            var elevationAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                $"{SourceFolder}/{regionName}_elevation.json");
            var osmAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                $"{SourceFolder}/{regionName}_osm.json");

            if (elevationAsset == null || osmAsset == null)
            {
                Debug.LogError($"[GeoImporter] Missing source JSON for '{regionName}' under {SourceFolder}.");
                return null;
            }

            ElevationJson elevation = JsonUtility.FromJson<ElevationJson>(elevationAsset.text);
            OsmJson osm = JsonUtility.FromJson<OsmJson>(osmAsset.text);

            if (elevation?.elevations == null || osm?.px == null)
            {
                Debug.LogError("[GeoImporter] Source JSON did not parse. Has its shape changed?");
                return null;
            }

            var points = new Vector2[osm.px.Length];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(osm.px[i], osm.py[i]);

            var buildings = new GeoBuilding[osm.buildings?.Length ?? 0];
            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingJson source = osm.buildings[i];
                buildings[i] = new GeoBuilding
                {
                    Centre = new Vector2(source.cx, source.cy),
                    Size = new Vector2(source.sx, source.sy),
                    RotationDegrees = source.rot,
                    Height = source.h,
                    Archetype = (BuildingArchetype)source.arch,
                    PointStart = source.ps,
                    PointCount = source.pc,
                };
            }

            var roads = new GeoWay[osm.roads?.Length ?? 0];
            for (int i = 0; i < roads.Length; i++)
            {
                RoadJson source = osm.roads[i];
                roads[i] = new GeoWay
                {
                    PointStart = source.ps,
                    PointCount = source.pc,
                    Class = (RoadClass)source.cls,
                    Width = source.w,
                };
            }

            ProjectPaths.EnsureFolder(DatasetFolder);
            string path = $"{DatasetFolder}/GeoDataset_{regionName}.asset";

            var dataset = AssetDatabase.LoadAssetAtPath<GeoDataset>(path);
            if (dataset == null)
            {
                dataset = ScriptableObject.CreateInstance<GeoDataset>();
                AssetDatabase.CreateAsset(dataset, path);
            }

            dataset.Populate(regionName, osm.originLat, osm.originLon, osm.sizeX, osm.sizeZ,
                             elevation.resolution, elevation.elevations,
                             buildings, roads,
                             Convert(osm.water), Convert(osm.parks), points);

            EditorUtility.SetDirty(dataset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GeoImporter] '{regionName}': {buildings.Length} edificios, {roads.Length} calles, " +
                      $"{dataset.Water.Length} de agua, {dataset.Parks.Length} parques. " +
                      $"Elevacion {dataset.MinHeight:F0}-{dataset.MaxHeight:F0} m sobre " +
                      $"{dataset.SizeX:F0}x{dataset.SizeZ:F0} m.");

            Selection.activeObject = dataset;
            return dataset;
        }

        static GeoArea[] Convert(AreaJson[] source)
        {
            var result = new GeoArea[source?.Length ?? 0];
            for (int i = 0; i < result.Length; i++)
                result[i] = new GeoArea { PointStart = source[i].ps, PointCount = source[i].pc };
            return result;
        }
    }
}
