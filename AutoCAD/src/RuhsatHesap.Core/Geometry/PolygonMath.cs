using System.Collections.Generic;

namespace RuhsatHesap.Core.Geometry
{
    /// <summary>
    /// Plain point-in-polygon math with no CAD dependency, so the containment
    /// logic that decides whether a %30/emsal dışı alan sits inside an emsal
    /// sınırı can be unit tested without an AutoCAD assembly. The AutoCAD-side
    /// GeometryUtil.PointInPolygon uses the same ray-casting algorithm on its
    /// own Point2d type; this is its geometry-free twin.
    /// </summary>
    public static class PolygonMath
    {
        public static bool PointInPolygon (IReadOnlyList<(double X, double Y)> polygon, double pointX, double pointY)
        {
            if (polygon == null || polygon.Count < 3) return false;
            bool inside = false;
            for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++) {
                (double X, double Y) current = polygon[index];
                (double X, double Y) last = polygon[previous];
                if (current.Y > pointY != last.Y > pointY) {
                    double intersectX = (last.X - current.X) * (pointY - current.Y) / (last.Y - current.Y) + current.X;
                    if (pointX < intersectX) inside = !inside;
                }
            }
            return inside;
        }
    }
}
