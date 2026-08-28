using System.Collections.Generic;
using UnityEngine;

namespace Survival.GeoData
{
    /// <summary>
    /// Ear clipping for simple polygons. Enough for OSM areas -- riverbanks and parks are
    /// concave but do not self-intersect, and the modular building system will want the same
    /// thing for real footprints.
    /// <para>
    /// Does not handle holes. A park with a lake inside it comes out solid. Worth knowing before
    /// trusting it for anything beyond placeholders.
    /// </para>
    /// </summary>
    public static class PolygonTriangulator
    {
        public static bool Triangulate(List<Vector2> polygon, List<int> triangles)
        {
            int count = polygon.Count;
            if (count < 3) return false;

            var remaining = new List<int>(count);

            // Work in counter-clockwise order so the "is this ear convex" test has one answer.
            bool clockwise = SignedArea(polygon) < 0f;
            for (int i = 0; i < count; i++) remaining.Add(clockwise ? count - 1 - i : i);

            int guard = count * count;   // a self-intersecting ring would otherwise spin forever

            while (remaining.Count > 2 && guard-- > 0)
            {
                bool clipped = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    int previous = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int current = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];

                    if (!IsEar(polygon, remaining, previous, current, next)) continue;

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped) return triangles.Count > 0; // degenerate: keep whatever was valid
            }

            return triangles.Count > 0;
        }

        static bool IsEar(List<Vector2> polygon, List<int> remaining, int a, int b, int c)
        {
            Vector2 pa = polygon[a];
            Vector2 pb = polygon[b];
            Vector2 pc = polygon[c];

            if (Cross(pb - pa, pc - pa) <= 0f) return false; // reflex, not an ear

            foreach (int index in remaining)
            {
                if (index == a || index == b || index == c) continue;
                if (PointInTriangle(polygon[index], pa, pb, pc)) return false;
            }

            return true;
        }

        static float SignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, point - a);
            float d2 = Cross(c - b, point - b);
            float d3 = Cross(a - c, point - c);

            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }
    }
}
