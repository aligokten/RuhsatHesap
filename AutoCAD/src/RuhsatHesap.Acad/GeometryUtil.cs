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
                        closed = true;
                        return TryGetHatchArea (hatch, out area);
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
            } catch (System.Exception) {
                // Area throws for non-planar, degenerate or self-intersecting
                // outlines, and the exception type differs per entity class.
                // Whatever the reason, the object is simply unmeasurable and
                // the caller reports it as such.
                return false;
            }
        }

        /// <summary>
        /// Hatch.Area is not available for every hatch -- non-associative
        /// hatches and hatches whose loops are not simple polylines throw
        /// instead of returning a value. Those are common in plans where the
        /// alan is drawn by picking a point inside the walls, so the boundary
        /// loops are measured directly as a fallback.
        /// </summary>
        private static bool TryGetHatchArea (Hatch hatch, out double area)
        {
            area = 0.0;
            try {
                area = Math.Abs (hatch.Area);
                if (area > 1e-9) return true;
            } catch (System.Exception) {
                area = 0.0;
            }

            try {
                double total = 0.0;
                for (int index = 0; index < hatch.NumberOfLoops; index++) {
                    HatchLoop loop = hatch.GetLoopAt (index);
                    double loopArea = LoopArea (loop);
                    // Outer boundaries add, islands cut out.
                    bool isOuter = (loop.LoopType & HatchLoopTypes.External) != 0 ||
                                   (loop.LoopType & HatchLoopTypes.Outermost) != 0 ||
                                   hatch.NumberOfLoops == 1;
                    total += isOuter ? loopArea : -loopArea;
                }
                area = Math.Abs (total);
                return area > 1e-9;
            } catch (System.Exception) {
                area = 0.0;
                return false;
            }
        }

        private static double LoopArea (HatchLoop loop) => Math.Abs (ShoelaceArea (LoopPoints (loop)));

        private static List<Point2d> LoopPoints (HatchLoop loop)
        {
            var points = new List<Point2d> ();
            if ((loop.LoopType & HatchLoopTypes.Polyline) != 0) {
                foreach (BulgeVertex vertex in loop.Polyline) points.Add (vertex.Vertex);
            } else {
                foreach (Curve2d curve in loop.Curves) {
                    points.Add (curve.StartPoint);
                    // One extra point per curve keeps arcs from being cut down
                    // to their chord.
                    try {
                        Interval interval = curve.GetInterval ();
                        points.Add (curve.EvaluatePoint ((interval.LowerBound + interval.UpperBound) * 0.5));
                    } catch (System.Exception) {
                        // Straight segments need no mid point.
                    }
                }
            }
            return points;
        }

        /// <summary>
        /// The hatch's outer boundary as a polygon, ignoring island (cut-out)
        /// loops -- good enough for point-in-polygon containment tests, which
        /// only need the outer shape. Only the first outer loop is used, so a
        /// hatch made of several disjoint regions is approximated by just one
        /// of them; RHTARA has no use case yet where that distinction matters.
        /// </summary>
        private static List<Point2d> HatchOuterLoopPolygon (Hatch hatch)
        {
            try {
                for (int index = 0; index < hatch.NumberOfLoops; index++) {
                    HatchLoop loop = hatch.GetLoopAt (index);
                    bool isOuter = (loop.LoopType & HatchLoopTypes.External) != 0 ||
                                   (loop.LoopType & HatchLoopTypes.Outermost) != 0 ||
                                   hatch.NumberOfLoops == 1;
                    if (!isOuter) continue;
                    List<Point2d> points = LoopPoints (loop);
                    if (points.Count >= 3) return points;
                }
            } catch (System.Exception) {
                // Falls through to an empty polygon; SamplePolygon then uses
                // the bounding-box fallback.
            }
            return new List<Point2d> ();
        }

        public static double ShoelaceArea (IList<Point2d> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0.0;
            double twiceArea = 0.0;
            for (int index = 0; index < polygon.Count; index++) {
                Point2d current = polygon[index];
                Point2d next = polygon[(index + 1) % polygon.Count];
                twiceArea += current.X * next.Y - next.X * current.Y;
            }
            return twiceArea * 0.5;
        }

        public static bool TryGetExtents (Entity entity, out Extents3d extents)
        {
            extents = new Extents3d ();
            try {
                extents = entity.GeometricExtents;
                return true;
            } catch (System.Exception) {
                // Empty blocks and degenerate entities have no extents.
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
            // Hatch is neither a Curve nor a Polyline, so without this it fell
            // straight through to the bounding-box fallback below -- wrong for
            // an L-shaped or otherwise concave hatched alan (a very common
            // case: users hatch the floor fill and etiketler the hatch), since
            // its bounding box reaches past the true outline and can make an
            // unrelated nearby object look contained.
            if (entity is Hatch hatch) {
                List<Point2d> loopPoints = HatchOuterLoopPolygon (hatch);
                if (loopPoints.Count >= 3) return loopPoints;
                // Falls through to the bounding-box fallback for hatches whose
                // loops could not be read (e.g. non-associative hatches).
            }

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
                } catch (System.Exception) {
                    // Fall through to the bounding box below.
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
