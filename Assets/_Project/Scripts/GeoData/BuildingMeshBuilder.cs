using System.Collections.Generic;
using UnityEngine;

namespace Survival.GeoData
{
    /// <summary>
    /// Builds meshes for both LOD 0 (fast batch solid exterior) and LOD 1 (explorable building
    /// with walk-in door, interior floor slabs, stairwells and stairs).
    /// </summary>
    public static class BuildingMeshBuilder
    {
        public const float DefaultDoorWidth = 1.4f;
        public const float DefaultDoorHeight = 2.2f;

        /// <summary>
        /// Extrudes a solid exterior building (LOD 0) identical to the Cesium massing model,
        /// but with guaranteed minimum height.
        /// </summary>
        public static void AddSolidBuilding(List<Vector3> vertices, List<Color> colors, List<int> triangles,
                                           in GeoBuilding building, float groundY, float foundationDepth,
                                           float minHeight, Color wallColor)
        {
            float radians = building.RotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            Vector2 half = building.Size * 0.5f;
            float rawHeight = building.Height > 0.1f ? building.Height * 1.5f : minHeight;
            float height = Mathf.Max(minHeight, rawHeight);
            float bottom = groundY - foundationDepth;
            float top = groundY + height;

            var corners = GetRotatedCorners(building.Centre, half, cos, sin);

            // 4 walls
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i];
                Vector3 b = corners[(i + 1) % 4];

                AddDoubleSidedQuad(vertices, colors, triangles,
                                  new Vector3(a.x, bottom, a.z),
                                  new Vector3(b.x, bottom, b.z),
                                  new Vector3(b.x, top, b.z),
                                  new Vector3(a.x, top, a.z),
                                  wallColor);
            }

            // Roof
            Color roofColour = Color.Lerp(wallColor, Color.white, 0.18f);
            AddDoubleSidedQuad(vertices, colors, triangles,
                              new Vector3(corners[0].x, top, corners[0].z),
                              new Vector3(corners[1].x, top, corners[1].z),
                              new Vector3(corners[2].x, top, corners[2].z),
                              new Vector3(corners[3].x, top, corners[3].z),
                              roofColour);
        }

        /// <summary>
        /// Builds an explorable interior building (LOD 1) with an open ground floor doorway,
        /// interior floor slabs, stair openings, and physical stairs.
        /// </summary>
        public static void AddExplorableBuilding(List<Vector3> vertices, List<Color> colors, List<int> triangles,
                                                in GeoBuilding building, float groundY, float foundationDepth,
                                                float minHeight, float floorHeight, Color wallColor)
        {
            float radians = building.RotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            Vector2 half = building.Size * 0.5f;
            float rawHeight = building.Height > 0.1f ? building.Height * 1.5f : minHeight;
            float height = Mathf.Max(minHeight, rawHeight);
            int floorCount = Mathf.Max(2, Mathf.RoundToInt(height / floorHeight));
            float actualFloorHeight = height / floorCount;

            float bottom = groundY - foundationDepth;
            float top = groundY + height;

            var corners = GetRotatedCorners(building.Centre, half, cos, sin);

            // 1. Walls: Wall 0 has the open doorway, Walls 1..3 are solid
            BuildDoorwayWall(vertices, colors, triangles, corners[0], corners[1], groundY, bottom, top,
                             DefaultDoorWidth, DefaultDoorHeight, wallColor);

            for (int i = 1; i < 4; i++)
            {
                Vector3 a = corners[i];
                Vector3 b = corners[(i + 1) % 4];
                AddDoubleSidedQuad(vertices, colors, triangles,
                                  new Vector3(a.x, bottom, a.z),
                                  new Vector3(b.x, bottom, b.z),
                                  new Vector3(b.x, top, b.z),
                                  new Vector3(a.x, top, a.z),
                                  wallColor);
            }

            // 2. Interior Floor Slabs & Stairs
            Color floorColor = Color.Lerp(wallColor, Color.black, 0.15f);
            Color stairColor = new Color(0.35f, 0.32f, 0.28f);

            Vector3 stairDir = (corners[2] - corners[1]).normalized;
            Vector3 stairRight = (corners[1] - corners[0]).normalized;
            Vector3 stairOrigin = corners[0] + stairRight * 1.5f + stairDir * 1.0f;

            for (int f = 1; f < floorCount; f++)
            {
                float floorY = groundY + f * actualFloorHeight;

                // Add floor slab quad
                AddDoubleSidedQuad(vertices, colors, triangles,
                                  new Vector3(corners[0].x, floorY, corners[0].z),
                                  new Vector3(corners[1].x, floorY, corners[1].z),
                                  new Vector3(corners[2].x, floorY, corners[2].z),
                                  new Vector3(corners[3].x, floorY, corners[3].z),
                                  floorColor);

                // Add staircase from floor below to current floor
                float prevFloorY = groundY + (f - 1) * actualFloorHeight;
                StaircaseBuilder.AddStairs(vertices, colors, triangles,
                                          stairOrigin, stairDir, prevFloorY, floorY,
                                          StaircaseBuilder.DefaultStairWidth, stairColor);
            }

            // 3. Roof slab
            Color roofColour = Color.Lerp(wallColor, Color.white, 0.18f);
            AddDoubleSidedQuad(vertices, colors, triangles,
                              new Vector3(corners[0].x, top, corners[0].z),
                              new Vector3(corners[1].x, top, corners[1].z),
                              new Vector3(corners[2].x, top, corners[2].z),
                              new Vector3(corners[3].x, top, corners[3].z),
                              roofColour);
        }

        static void BuildDoorwayWall(List<Vector3> vertices, List<Color> colors, List<int> triangles,
                                    Vector3 a, Vector3 b, float groundY, float bottomY, float topY,
                                    float doorWidth, float doorHeight, Color color)
        {
            Vector3 dir = (b - a).normalized;
            float wallLen = Vector3.Distance(a, b);

            float doorStart = Mathf.Max(0.5f, (wallLen - doorWidth) * 0.5f);
            float doorEnd = Mathf.Min(wallLen - 0.5f, doorStart + doorWidth);

            Vector3 pDoorLeft = a + dir * doorStart;
            Vector3 pDoorRight = a + dir * doorEnd;
            float lintelY = groundY + doorHeight;

            // Left wall segment
            AddDoubleSidedQuad(vertices, colors, triangles,
                              new Vector3(a.x, bottomY, a.z),
                              new Vector3(pDoorLeft.x, bottomY, pDoorLeft.z),
                              new Vector3(pDoorLeft.x, topY, pDoorLeft.z),
                              new Vector3(a.x, topY, a.z),
                              color);

            // Right wall segment
            AddDoubleSidedQuad(vertices, colors, triangles,
                              new Vector3(pDoorRight.x, bottomY, pDoorRight.z),
                              new Vector3(b.x, bottomY, b.z),
                              new Vector3(b.x, topY, b.z),
                              new Vector3(pDoorRight.x, topY, pDoorRight.z),
                              color);

            // Lintel segment above door (doorway below remains open)
            AddDoubleSidedQuad(vertices, colors, triangles,
                              new Vector3(pDoorLeft.x, lintelY, pDoorLeft.z),
                              new Vector3(pDoorRight.x, lintelY, pDoorRight.z),
                              new Vector3(pDoorRight.x, topY, pDoorRight.z),
                              new Vector3(pDoorLeft.x, topY, pDoorLeft.z),
                              color);
        }

        static void AddDoubleSidedQuad(List<Vector3> vertices, List<Color> colors, List<int> triangles,
                                      Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color)
        {
            int baseIndex = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);

            for (int i = 0; i < 4; i++) colors.Add(color);

            // Front face (Clockwise)
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 2);

            // Back face (Counter-Clockwise)
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        static Vector3[] GetRotatedCorners(Vector2 centre, Vector2 half, float cos, float sin)
        {
            var corners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = (i == 0 || i == 3) ? -half.x : half.x;
                float sz = (i < 2) ? -half.y : half.y;
                corners[i] = new Vector3(
                    centre.x + sx * cos - sz * sin,
                    0f,
                    centre.y + sx * sin + sz * cos);
            }
            return corners;
        }
    }
}
