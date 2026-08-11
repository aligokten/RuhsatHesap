using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace RuhsatHesap.Acad
{
    /// <summary>
    /// Area and containment helpers. Everything the plug-in measures goes
    /// through here, so a closed polyline, a circle, a region and a hatch are
    /// all handled the same way.
    /// </summary>
    public static class GeometryUtil
    {
        private const int SampleCount = 96;

        /// <summary>Entity types RHTARA and the area tables accept.</summary>
        public static readonly string[] SupportedDxfNames =
            { "LWPOLYLINE", "POLYLINE", "CIRCLE", "ELLIPSE", "REGION", "HATCH", "SPLINE" };

        /// <summary>
        /// Reads the enclosed area in drawing units squared. <paramref name="closed"/>
        /// reports whether the outline really is closed; an open polyline still
        /// yields the area of its implied closing segment, which is worth
        /// warning about rather than silently accepting.
        /// </summary>
        public static bool TryGetArea (Entity entity, out double area, out bool closed)
        {
            area = 0.0;
            closed = false;
            try {
                switch (entity) {
                    case Hatch hatch:
                        area = Math.Abs (hatch.Area);
                        closed = true;
                        return area > 0.0;
                    case Region region:
                        area = Math.Abs (region.Area);
                        closed = true;
                        return area > 0.0;
                    case Curve curve:
                        closed = curve.Closed;
                        area = Math.Abs (curve.Area);
                        return area > 0.0;
                    default:
                        return false;
                }
            } catch (Autodesk.AutoCAD.Runtime.Exception) {
                // Curve.Area throws for self-intersecting or non-planar
                // outlines; those are reported as unmeasurable.
                return false;
            } catch (InvalidOperationException) {
                return false;
            }
        }

        public static bool TryGetExtents (Entity entity, out Extents3d extents)
        {
            extents = new Extents3d ();
            try {
                extents = entity.GeometricExtents;
                return true;
            } catch (Autodesk.AutoCAD.Runtime.Exception) {
                return false;
            }
        }

        /// <summary>A point that lies inside the shape well enough to test
        /// which kat sınırı frame contains it, and to place its label.</summary>
        public static Point3d RepresentativePoint (Entity entity)
        {
            List<Point2d> polygon = SamplePolygon (entity);
            double elevation = 0.0;
            if (TryGetExtents (entity, out Extents3d extents))
                elevation = (extents.MinPoint.Z + extents.MaxPoint.Z) * 0.5;

            if (polygon.Count >= 3) {
                Point2d centroid = PolygonCentroid (polygon);
                if (PointInPolygon (polygon, centroid)) return new Point3d (centroid.X, centroid.Y, elevation);
                // Concave outline: fall back to the centre of the bounding box,
                // which is still inside the surrounding floor frame.
            }
            if (TryGetExtents (entity, out Extents3d box)) {
                return new Point3d ((box.MinPoint.X + box.MaxPoint.X) * 0.5,
                                    (box.MinPoint.Y + box.MaxPoint.Y) * 0.5,
                                    elevation);
            }
            return Point3d.Origin;
        }

        /// <summary>
        /// Approximates the outline as a polygon. Curves are sampled by length
        /// so arcs, circles and splines all work.
        /// </summary>
        public static List<Point2d> SamplePolygon (Entity entity)
        {
            var points = new List<Point2d> ();
            if (entity is Polyline lightweight && !HasBulge (lightweight)) {
                for (int index = 0; index < lightweight.NumberOfVertices; index++) {
                    Point2d vertex = lightweight.GetPoint2dAt (index);
                    points.Add (vertex);
                }
                return points;
            }

            if (entity is Curve curve) {
                try {
                    double length = curve.GetDistanceAtParameter (curve.EndParam);
                    if (length > 1e-9) {
                        for (int index = 0; index < SampleCount; index++) {
                            Point3d point = curve.GetPointAtDist (length * index / SampleCount);
                            points.Add (new Point2d (point.X, point.Y));
                        }
                        return points;
                    }
                } catch (Autodesk.AutoCAD.Runtime.Exception) {
                    points.Clear ();
                }
            }

            if (TryGetExtents (entity, out Extents3d extents)) {
                points.Add (new Point2d (extents.MinPoint.X, extents.MinPoint.Y));
                points.Add (new Point2d (extents.MaxPoint.X, extents.MinPoint.Y));
                points.Add (new Point2d (extents.MaxPoint.X, extents.MaxPoint.Y));
                points.Add (new Point2d (extents.MinPoint.X, extents.MaxPoint.Y));
            }
            return points;
        }

        private static bool HasBulge (Polyline polyline)
        {
            for (int index = 0; index < polyline.NumberOfVertices; index++)
                if (Math.Abs (polyline.GetBulgeAt (index)) > 1e-12) return true;
            return false;
        }

        public static Point2d PolygonCentroid (IList<Point2d> polygon)
        {
            double twiceArea = 0.0, x = 0.0, y = 0.0;
            for (int index = 0; index < polygon.Count; index++) {
                Point2d current = polygon[index];
                Point2d next = polygon[(index + 1) % polygon.Count];
                double cross = current.X * next.Y - next.X * current.Y;
                twiceArea += cross;
                x += (current.X + next.X) * cross;
                y += (current.Y + next.Y) * cross;
            }
            if (Math.Abs (twiceArea) < 1e-12) {
                double sumX = 0.0, sumY = 0.0;
                foreach (Point2d point in polygon) { sumX += point.X; sumY += point.Y; }
                int count = Math.Max (1, polygon.Count);
                return new Point2d (sumX / count, sumY / count);
            }
            return new Point2d (x / (3.0 * twiceArea), y / (3.0 * twiceArea));
        }

        /// <summary>Ray casting containment test.</summary>
        public static bool PointInPolygon (IList<Point2d> polygon, Point2d point)
        {
            if (polygon == null || polygon.Count < 3) return false;
            bool inside = false;
            for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++) {
                Point2d current = polygon[index];
                Point2d last = polygon[previous];
                if (current.Y > point.Y != last.Y > point.Y) {
                    double intersectX = (last.X - current.X) * (point.Y - current.Y) / (last.Y - current.Y) + current.X;
                    if (point.X < intersectX) inside = !inside;
                }
            }
            return inside;
        }

        public static bool ExtentsContain (Extents3d outer, Point3d point) =>
            point.X >= outer.MinPoint.X && point.X <= outer.MaxPoint.X &&
            point.Y >= outer.MinPoint.Y && point.Y <= outer.MaxPoint.Y;
    }
}
