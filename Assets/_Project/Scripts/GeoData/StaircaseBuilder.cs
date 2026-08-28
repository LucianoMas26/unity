using System.Collections.Generic;
using UnityEngine;

namespace Survival.GeoData
{
    /// <summary>
    /// Generates physical stairs connecting two vertical floors inside an explorable building.
    /// </summary>
    public static class StaircaseBuilder
    {
        public const float DefaultStepHeight = 0.20f;
        public const float DefaultStairWidth = 1.20f;

        /// <summary>
        /// Builds a flight of stairs starting at <paramref name="start"/> on ground level <paramref name="bottomY"/>
        /// and climbing along <paramref name="forward"/> up to <paramref name="topY"/>.
        /// </summary>
        public static void AddStairs(List<Vector3> vertices, List<Color> colors, List<int> triangles,
                                     Vector3 start, Vector3 forward, float bottomY, float topY,
                                     float width, Color stepColor)
        {
            float totalHeight = topY - bottomY;
            if (totalHeight <= 0.1f) return;

            int stepCount = Mathf.Max(3, Mathf.RoundToInt(totalHeight / DefaultStepHeight));
            float actualStepHeight = totalHeight / stepCount;
            float stepDepth = 0.30f;

            Vector3 fwd = forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized * (width * 0.5f);

            for (int s = 0; s < stepCount; s++)
            {
                float y0 = bottomY + s * actualStepHeight;
                float y1 = y0 + actualStepHeight;

                Vector3 p0 = start + fwd * (s * stepDepth);
                Vector3 p1 = start + fwd * ((s + 1) * stepDepth);

                // 1. Step Riser (vertical face)
                int vRiser = vertices.Count;
                vertices.Add(new Vector3(p0.x - right.x, y0, p0.z - right.z));
                vertices.Add(new Vector3(p0.x + right.x, y0, p0.z + right.z));
                vertices.Add(new Vector3(p0.x + right.x, y1, p0.z + right.z));
                vertices.Add(new Vector3(p0.x - right.x, y1, p0.z - right.z));

                for (int i = 0; i < 4; i++) colors.Add(stepColor);
                triangles.Add(vRiser); triangles.Add(vRiser + 2); triangles.Add(vRiser + 1);
                triangles.Add(vRiser); triangles.Add(vRiser + 3); triangles.Add(vRiser + 2);

                // 2. Step Tread (horizontal flat face)
                int vTread = vertices.Count;
                vertices.Add(new Vector3(p0.x - right.x, y1, p0.z - right.z));
                vertices.Add(new Vector3(p0.x + right.x, y1, p0.z + right.z));
                vertices.Add(new Vector3(p1.x + right.x, y1, p1.z + right.z));
                vertices.Add(new Vector3(p1.x - right.x, y1, p1.z - right.z));

                for (int i = 0; i < 4; i++) colors.Add(stepColor);
                triangles.Add(vTread); triangles.Add(vTread + 2); triangles.Add(vTread + 1);
                triangles.Add(vTread); triangles.Add(vTread + 3); triangles.Add(vTread + 2);
            }
        }
    }
}
