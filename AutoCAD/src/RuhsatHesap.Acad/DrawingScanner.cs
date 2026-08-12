using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using RuhsatHesap.Core.Tagging;

namespace RuhsatHesap.Acad
{
    public sealed class ScanStats
    {
        public int Examined;
        public int Tagged;
        public int Untagged;
        public int Unmeasurable;
        public int OpenOutlines;
        public int FloorFrames;
        public readonly List<string> Warnings = new List<string> ();

        public void Warn (string message)
        {
            if (Warnings.Count < 20 && !Warnings.Contains (message)) Warnings.Add (message);
        }
    }

    /// <summary>
    /// Turns drawing objects into <see cref="AreaObservation"/> records: reads
    /// the etiket, measures the area in square metres and resolves which kat
    /// the object belongs to.
    /// </summary>
    public static class DrawingScanner
    {
        private sealed class FloorFrame
        {
            public string FloorName = string.Empty;
            public string BlockName = string.Empty;
            public List<Point2d> Polygon = new List<Point2d> ();
            public Extents3d Extents;
            public bool HasExtents;
            public double Size;
        }

        private sealed class LooseLabel
        {
            public string TagText = string.Empty;
            public Point3d Position;
            public bool Used;
        }

        public static IReadOnlyList<ObjectId> ModelSpaceIds (Database database, Transaction transaction)
        {
            var ids = new List<ObjectId> ();
            var blockTable = (BlockTable) transaction.GetObject (database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord) transaction.GetObject (blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in modelSpace) ids.Add (id);
            return ids;
        }

        /// <summary>
        /// Reads every measurable object in <paramref name="ids"/>.
        /// </summary>
        /// <param name="includeUntagged">
        /// true for the free area tables (RHALANTABLO), where any selected
        /// polyline counts; false for the project sync, which must only pick up
        /// objects carrying an RH etiketi.
        /// </param>
        public static List<AreaObservation> Collect (Database database, Transaction transaction,
            IEnumerable<ObjectId> ids, DrawingSettings settings, bool includeUntagged, ScanStats stats)
        {
            var idList = ids.Where (id => !id.IsNull && !id.IsErased).ToList ();
            var entities = new List<Entity> ();
            foreach (ObjectId id in idList) {
                var entity = transaction.GetObject (id, OpenMode.ForRead, false, true) as Entity;
                if (entity != null) entities.Add (entity);
            }

            List<FloorFrame> frames = CollectFloorFrames (entities, stats);
            List<LooseLabel> labels = CollectLooseLabels (entities);

            double factor = settings.AreaFactor;
            var observations = new List<AreaObservation> ();

            foreach (Entity entity in entities) {
                if (entity is DBText || entity is MText || entity is Table) continue;

                string tagText = TagStorage.ReadTag (entity);
                bool fromXData = tagText.Length > 0;
                RuhsatTag tag = fromXData ? RuhsatTag.Parse (tagText) : null;
                if (tag != null && tag.Kind == AreaKind.FloorFrame) continue;

                if (!GeometryUtil.TryGetArea (entity, out double rawArea, out bool closed)) {
                    // An etiketli object that cannot be measured is the single
                    // most confusing failure -- it silently drops out of every
                    // table -- so it is always reported.
                    if (fromXData || TagStorage.TryParseLayerTag (entity.Layer, out string unusedLayerTag)) {
                        stats.Unmeasurable++;
                        stats.Warn ("<" + entity.Handle + "> (" + entity.GetType ().Name + ", " + entity.Layer +
                            ") ölçülemedi: kapalı bir alan vermiyor, etiketi hesaba girmedi.");
                    }
                    continue;
                }
                stats.Examined++;

                Point3d anchor = GeometryUtil.RepresentativePoint (entity);

                if (!fromXData) {
                    LooseLabel label = FindLabelInside (labels, entity, anchor);
                    if (label != null) {
                        tagText = label.TagText;
                        label.Used = true;
                    } else if (TagStorage.TryParseLayerTag (entity.Layer, out string layerTag)) {
                        tagText = layerTag;
                    }
                    tag = RuhsatTag.Parse (tagText);
                    if (tag.Kind == AreaKind.FloorFrame) continue;
                }

                bool tagged = tag != null && tag.IsRuhsatTag;
                if (tagged) stats.Tagged++; else stats.Untagged++;
                if (!tagged && !includeUntagged) continue;

                if (!closed) {
                    stats.OpenOutlines++;
                    stats.Warn ("<" + entity.Handle + "> kapalı değil; alan kapatılmış varsayılarak hesaplandı.");
                }

                var observation = new AreaObservation {
                    TagText = tagText,
                    Area = Math.Round (rawArea * factor, 4),
                    Handle = entity.Handle.ToString (),
                    Layer = entity.Layer,
                    FloorName = ResolveFloor (tag, frames, anchor, settings),
                    AnchorX = anchor.X,
                    AnchorY = anchor.Y
                };
                if (tag != null) observation.SetTag (tag);

                // A TIP=EMSAL sınırı's outline is what RuhsatHesap.Core needs
                // to subtract a nested %30/emsal dışı alan from it. Whether a
                // serbest TIP will turn into CustomFloorArea (and so could
                // later act as one of those nested kalemler) is not knowable
                // this early -- that promotion happens once ProjectData is
                // available, inside TagSync.Sync -- so every floor-level,
                // non-unit kalem is sampled to be safe.
                if (tag != null && tag.IsRuhsatTag && !tag.IsUnitArea && tag.NeedsFloor) {
                    List<Point2d> polygon = GeometryUtil.SamplePolygon (entity);
                    if (polygon.Count >= 3) {
                        var points = new (double X, double Y)[polygon.Count];
                        for (int index = 0; index < polygon.Count; index++)
                            points[index] = (polygon[index].X, polygon[index].Y);
                        observation.Polygon = points;
                    }
                }

                observations.Add (observation);
            }

            return observations;
        }

        private static string ResolveFloor (RuhsatTag tag, List<FloorFrame> frames, Point3d anchor, DrawingSettings settings)
        {
            if (tag != null && tag.FloorName.Trim ().Length > 0) return tag.FloorName.Trim ();
            FloorFrame frame = FindFrame (frames, anchor);
            if (frame != null) return frame.FloorName;
            return settings.ActiveFloor;
        }

        /// <summary>
        /// Kat sınırı frames may be nested (a whole floor plan and a detail
        /// inside it), so the smallest frame containing the point wins.
        /// </summary>
        private static FloorFrame FindFrame (List<FloorFrame> frames, Point3d point)
        {
            FloorFrame best = null;
            var point2d = new Point2d (point.X, point.Y);
            foreach (FloorFrame frame in frames) {
                if (frame.HasExtents && !GeometryUtil.ExtentsContain (frame.Extents, point)) continue;
                if (frame.Polygon.Count >= 3 && !GeometryUtil.PointInPolygon (frame.Polygon, point2d)) continue;
                if (best == null || frame.Size < best.Size) best = frame;
            }
            return best;
        }

        private static List<FloorFrame> CollectFloorFrames (List<Entity> entities, ScanStats stats)
        {
            var frames = new List<FloorFrame> ();
            foreach (Entity entity in entities) {
                string tagText = TagStorage.ReadTag (entity);
                if (tagText.Length == 0 && !TagStorage.TryParseLayerTag (entity.Layer, out tagText)) continue;
                RuhsatTag tag = RuhsatTag.Parse (tagText);
                if (!tag.IsRuhsatTag || tag.Kind != AreaKind.FloorFrame) continue;

                var frame = new FloorFrame {
                    FloorName = tag.FloorName.Trim (),
                    BlockName = tag.BlockName,
                    Polygon = GeometryUtil.SamplePolygon (entity)
                };
                frame.HasExtents = GeometryUtil.TryGetExtents (entity, out Extents3d extents);
                frame.Extents = extents;
                frame.Size = frame.HasExtents
                    ? Math.Abs (extents.MaxPoint.X - extents.MinPoint.X) * Math.Abs (extents.MaxPoint.Y - extents.MinPoint.Y)
                    : double.MaxValue;
                if (frame.FloorName.Length == 0) {
                    stats.Warn ("<" + entity.Handle + "> kat sınırında KAT= değeri yok; yok sayıldı.");
                    continue;
                }
                frames.Add (frame);
            }
            stats.FloorFrames = frames.Count;
            return frames;
        }

        private static List<LooseLabel> CollectLooseLabels (List<Entity> entities)
        {
            var labels = new List<LooseLabel> ();
            foreach (Entity entity in entities) {
                string content = null;
                Point3d position = Point3d.Origin;
                if (entity is DBText text) {
                    content = text.TextString;
                    position = text.Position;
                } else if (entity is MText mtext) {
                    content = TagStorage.StripMTextFormatting (mtext.Contents);
                    position = mtext.Location;
                }
                if (content == null || !TagStorage.LooksLikeTag (content)) continue;
                labels.Add (new LooseLabel { TagText = content.Trim (), Position = position });
            }
            return labels;
        }

        private static LooseLabel FindLabelInside (List<LooseLabel> labels, Entity entity, Point3d anchor)
        {
            if (labels.Count == 0) return null;
            List<Point2d> polygon = GeometryUtil.SamplePolygon (entity);
            if (polygon.Count < 3) return null;
            LooseLabel closest = null;
            double bestDistance = double.MaxValue;
            foreach (LooseLabel label in labels) {
                if (label.Used) continue;
                if (!GeometryUtil.PointInPolygon (polygon, new Point2d (label.Position.X, label.Position.Y))) continue;
                double distance = (label.Position - anchor).LengthSqrd;
                if (distance < bestDistance) {
                    bestDistance = distance;
                    closest = label;
                }
            }
            return closest;
        }
    }
}
