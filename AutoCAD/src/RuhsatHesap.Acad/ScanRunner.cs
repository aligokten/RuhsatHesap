using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Tagging;

namespace RuhsatHesap.Acad
{
    /// <summary>Everything one çizim taraması produced.</summary>
    public sealed class ScanOutcome
    {
        public ProjectData Project;
        public ScanStats Stats;
        public TagSyncResult Result;

        /// <summary>
        /// Geometry warnings and etiket problems in one list, in the order the
        /// user should read them. RHTARA prints these to the command line; the
        /// panel shows the same list in its own box, so a çift etiket uyarısı
        /// cannot be missed just because the scan was started from a button.
        /// </summary>
        public IReadOnlyList<string> Warnings
        {
            get {
                var all = new List<string> ();
                all.AddRange (Stats.Warnings);
                all.AddRange (Result.Problems);
                return all;
            }
        }
    }

    /// <summary>
    /// The çizim taraması itself, shared by the RHTARA command and the panel's
    /// "Çizimi Tara" button so both run exactly the same scan and see exactly
    /// the same warnings.
    /// </summary>
    public static class ScanRunner
    {
        public static ScanOutcome Run (Document document)
        {
            Database database = document.Database;
            DrawingSettings settings = DrawingStore.LoadSettings (database);
            ProjectData project = DrawingStore.LoadProject (database);
            var stats = new ScanStats ();
            List<AreaObservation> observations;

            using (document.LockDocument ())
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                observations = DrawingScanner.Collect (database, transaction,
                    DrawingScanner.ModelSpaceIds (database, transaction), settings, false, stats);
                transaction.Commit ();
            }

            TagSyncResult result = TagSync.Sync (project, observations);
            using (document.LockDocument ()) {
                DrawingStore.SaveProject (database, project);
            }

            return new ScanOutcome { Project = project, Stats = stats, Result = result };
        }
    }
}
