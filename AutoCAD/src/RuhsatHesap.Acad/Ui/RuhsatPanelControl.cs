using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
// Autodesk.AutoCAD.DatabaseServices is deliberately not imported here: it
// declares its own Font type, which would make every System.Drawing.Font in a
// Windows Forms control ambiguous. The panel talks to the drawing through
// DrawingStore instead.
using RuhsatHesap.Core;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Tagging;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace RuhsatHesap.Acad.Ui
{
    /// <summary>
    /// The Ruhsat Hesap form: parsel bilgileri, bağımsız bölümler ve katlar,
    /// filled in by hand instead of by tagging. It reads and writes the very
    /// same project data the etiket scan produces, so drawn and typed values
    /// live side by side — a re-scan rebuilds only what it owns.
    /// </summary>
    public sealed class RuhsatPanelControl : UserControl
    {
        private ProjectData _project = new ProjectData ();

        private readonly TextBox _projectName = NewText ();
        private readonly TextBox _city = NewText ();
        private readonly TextBox _district = NewText ();
        private readonly TextBox _neighborhood = NewText ();
        private readonly TextBox _blockNumber = NewText ();
        private readonly TextBox _parcelNumber = NewText ();
        private readonly TextBox _parcelArea = NewText ();
        private readonly TextBox _taksRate = NewText ();
        private readonly TextBox _kaksRate = NewText ();
        private readonly TextBox _directEmsal = NewText ();
        private readonly TextBox _footprint = NewText ();
        private readonly TextBox _parking = NewText ();
        private readonly ComboBox _emsalMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        private readonly TextBox _commonArea = NewText ();

        private readonly DataGridView _unitGrid = NewGrid ();
        private readonly DataGridView _floorGrid = NewGrid ();
        private readonly Label _status = new Label {
            Dock = DockStyle.Bottom, Height = 46, Padding = new Padding (8, 4, 8, 4),
            TextAlign = ContentAlignment.MiddleLeft
        };

        public RuhsatPanelControl ()
        {
            BuildUi ();
            LoadFromDrawing ();
        }

        // ------------------------------------------------------------------
        // Arayüz
        // ------------------------------------------------------------------

        private void BuildUi ()
        {
            Dock = DockStyle.Fill;
            BackColor = SystemColors.Control;
            Font = new Font ("Segoe UI", 8.5f);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add (BuildParcelTab ());
            tabs.TabPages.Add (BuildUnitTab ());
            tabs.TabPages.Add (BuildFloorTab ());

            Controls.Add (tabs);
            Controls.Add (_status);
            Controls.Add (BuildToolbar ());
        }

        private Control BuildToolbar ()
        {
            var bar = new FlowLayoutPanel {
                Dock = DockStyle.Top, Height = 34, Padding = new Padding (4, 4, 4, 0), WrapContents = false,
                AutoScroll = true
            };
            bar.Controls.Add (NewButton ("Yenile", "Çizimdeki veriyi forma yükler", (sender, args) => LoadFromDrawing ()));
            bar.Controls.Add (NewButton ("Kaydet", "Formdaki veriyi çizime yazar", (sender, args) => SaveToDrawing (true)));
            bar.Controls.Add (NewButton ("Çizimi Tara", "RHTARA — etiketli alanları okur", (sender, args) => RunCommand ("RHTARA")));
            bar.Controls.Add (NewButton ("Tabloları Çiz", "RHTABLOLAR", (sender, args) => RunCommand ("RHTABLOLAR")));
            bar.Controls.Add (NewButton ("Excel", "RHEXCEL", (sender, args) => RunCommand ("RHEXCEL")));
            bar.Controls.Add (NewButton ("JSON Kaydet", "RHJSONKAYDET", (sender, args) => RunCommand ("RHJSONKAYDET")));
            return bar;
        }

        private TabPage BuildParcelTab ()
        {
            var page = new TabPage ("Parsel") { Padding = new Padding (6), AutoScroll = true };
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, AutoSize = true
            };
            layout.ColumnStyles.Add (new ColumnStyle (SizeType.Absolute, 170));
            layout.ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 100));

            _emsalMethod.Items.AddRange (new object[] { "KAKS ile (Parsel × KAKS)", "Doğrudan emsal alanı" });
            _emsalMethod.SelectedIndex = 0;
            _emsalMethod.SelectedIndexChanged += (sender, args) => UpdateEmsalMethodFields ();

            AddRow (layout, "Proje adı", _projectName);
            AddRow (layout, "İl", _city);
            AddRow (layout, "İlçe", _district);
            AddRow (layout, "Mahalle", _neighborhood);
            AddRow (layout, "Ada", _blockNumber);
            AddRow (layout, "Parsel", _parcelNumber);
            AddRow (layout, "Parsel alanı (m²)", WithMeasureButton (_parcelArea, "Parsel sınırı polylineını seçin"));
            AddRow (layout, "TAKS oranı", _taksRate);
            AddRow (layout, "Emsal yöntemi", _emsalMethod);
            AddRow (layout, "KAKS / emsal oranı", _kaksRate);
            AddRow (layout, "Doğrudan emsal (m²)", _directEmsal);
            AddRow (layout, "Yapı oturum alanı (m²)", WithMeasureButton (_footprint, "Yapı oturum alanı polylineını seçin"));
            AddRow (layout, "Toplam ortak alan (m²)", _commonArea);
            AddRow (layout, "Projede ayrılan otopark", _parking);

            page.Controls.Add (layout);
            return page;
        }

        private TabPage BuildUnitTab ()
        {
            var page = new TabPage ("Bağımsız Bölümler") { Padding = new Padding (6) };
            AddGridColumn (_unitGrid, "Blok", 55);
            AddGridColumn (_unitGrid, "BB No", 55);
            AddGridColumn (_unitGrid, "Kat", 90);
            AddGridColumn (_unitGrid, "Nitelik", 90);
            AddGridColumn (_unitGrid, "Oda", 45);
            AddGridColumn (_unitGrid, "BB Brüt", 70);
            AddGridColumn (_unitGrid, "Eklenti Brüt", 80);
            AddGridColumn (_unitGrid, "BB Net", 70);
            AddGridColumn (_unitGrid, "Eklenti Net", 80);
            AddGridColumn (_unitGrid, "Balkon", 70);
            AddGridColumn (_unitGrid, "Arsa Payı", 70);
            AddGridColumn (_unitGrid, "Maliki", 110);

            page.Controls.Add (_unitGrid);
            page.Controls.Add (BuildGridButtons (_unitGrid, "Bağımsız bölüm ekle", row => {
                row.Cells[0].Value = DefaultBlockName ();
                row.Cells[1].Value = (_unitGrid.Rows.Count).ToString ();
                row.Cells[3].Value = "Mesken";
            }));
            return page;
        }

        private TabPage BuildFloorTab ()
        {
            var page = new TabPage ("Katlar / Emsal") { Padding = new Padding (6) };
            AddGridColumn (_floorGrid, "Blok", 55);
            AddGridColumn (_floorGrid, "Kat", 110);
            AddGridColumn (_floorGrid, "Emsal Alan", 85);
            AddGridColumn (_floorGrid, "Emsal Dışı", 85);
            AddGridColumn (_floorGrid, "%30 Toplam", 85, true);
            AddGridColumn (_floorGrid, "Yapı İnşaat", 85, true);

            page.Controls.Add (_floorGrid);
            page.Controls.Add (BuildGridButtons (_floorGrid, "Kat ekle", row => {
                row.Cells[0].Value = DefaultBlockName ();
            }));
            var note = new Label {
                Dock = DockStyle.Bottom, Height = 34, ForeColor = SystemColors.GrayText,
                Text = "%30 istisna ve yapı inşaat kalemleri etiketlerden okunur (TIP=MERDIVEN, HESAP=EMSAL …); " +
                       "burada toplamları görünür."
            };
            page.Controls.Add (note);
            return page;
        }

        private Control BuildGridButtons (DataGridView grid, string addText, Action<DataGridViewRow> initialise)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, WrapContents = false };
            bar.Controls.Add (NewButton (addText, null, (sender, args) => {
                int index = grid.Rows.Add ();
                initialise (grid.Rows[index]);
            }));
            bar.Controls.Add (NewButton ("Seçili satırı sil", null, (sender, args) => {
                foreach (DataGridViewRow row in grid.SelectedRows.Cast<DataGridViewRow> ().ToList ())
                    if (!row.IsNewRow) grid.Rows.Remove (row);
            }));
            return bar;
        }

        private Control WithMeasureButton (TextBox box, string prompt)
        {
            var host = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding (0) };
            box.Width = 120;
            host.Controls.Add (box);
            host.Controls.Add (NewButton ("Çizimden ölç", prompt, (sender, args) => MeasureInto (box, prompt)));
            return host;
        }

        private static TextBox NewText () => new TextBox { Width = 200, Margin = new Padding (0, 2, 0, 2) };

        private static DataGridView NewGrid () => new DataGridView {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            RowHeadersWidth = 26,
            BackgroundColor = SystemColors.Window
        };

        private static void AddGridColumn (DataGridView grid, string header, int width, bool readOnly = false)
        {
            grid.Columns.Add (new DataGridViewTextBoxColumn {
                HeaderText = header, Width = width, ReadOnly = readOnly, SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private static Button NewButton (string text, string tooltip, EventHandler onClick)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding (2) };
            button.Click += onClick;
            if (!string.IsNullOrEmpty (tooltip)) new ToolTip ().SetToolTip (button, tooltip);
            return button;
        }

        private static void AddRow (TableLayoutPanel layout, string label, Control field)
        {
            layout.Controls.Add (new Label {
                Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding (0, 6, 6, 0)
            });
            layout.Controls.Add (field);
        }

        private void UpdateEmsalMethodFields ()
        {
            bool direct = _emsalMethod.SelectedIndex == 1;
            _kaksRate.Enabled = !direct;
            _directEmsal.Enabled = direct;
        }

        private string DefaultBlockName ()
        {
            if (_project.Blocks.Count > 0) return _project.Blocks[0].Name;
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            return document == null ? "A" : DrawingStore.LoadSettings (document.Database).ActiveBlock;
        }

        // ------------------------------------------------------------------
        // Çizim ile alışveriş
        // ------------------------------------------------------------------

        public void LoadFromDrawing ()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) {
                SetStatus ("Açık çizim yok.");
                return;
            }
            _project = DrawingStore.LoadProject (document.Database);
            FillFormFromProject ();
            SetStatus (null);
        }

        public void SaveToDrawing (bool report)
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) {
                SetStatus ("Açık çizim yok.");
                return;
            }
            try {
                ApplyFormToProject ();
                using (DocumentLock documentLock = document.LockDocument ()) {
                    DrawingStore.SaveProject (document.Database, _project);
                }
                FillFormFromProject ();
                if (report) SetStatus ("Veriler çizime kaydedildi. (DWG'yi kaydetmeyi unutmayın.)");
                else SetStatus (null);
            } catch (System.Exception exception) {
                SetStatus ("Kaydedilemedi: " + exception.Message);
            }
        }

        /// <summary>
        /// Commands need the document context and often ask for a point, so the
        /// buttons queue the real command instead of doing the work here.
        /// </summary>
        private void RunCommand (string command)
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) {
                SetStatus ("Açık çizim yok.");
                return;
            }
            SaveToDrawing (false);
            document.SendStringToExecute (command + " ", true, false, true);
            SetStatus (command + " çalıştırıldı. Sonucu görmek için Yenile'ye basın.");
        }

        /// <summary>
        /// Runs a selection from a palette button. The drawing window has to
        /// take the focus first and the document has to be locked, otherwise
        /// the prompt has nowhere to read the pick from.
        /// </summary>
        private void MeasureInto (TextBox box, string prompt)
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            try {
                DrawingSettings settings = DrawingStore.LoadSettings (document.Database);
                AcadApp.MainWindow.Focus ();
                double? measured;
                using (DocumentLock documentLock = document.LockDocument ()) {
                    measured = Commands.SettingsCommands.MeasureSelection (document, settings, prompt);
                }
                if (measured == null) return;
                box.Text = TextUtil.FormatArea (measured.Value);
                SetStatus ("Ölçülen alan forma yazıldı: " + box.Text + " m²");
            } catch (System.Exception exception) {
                SetStatus ("Ölçülemedi: " + exception.Message);
            }
        }

        // ------------------------------------------------------------------
        // Form <-> ProjectData
        // ------------------------------------------------------------------

        private void FillFormFromProject ()
        {
            ParcelInfo parcel = _project.Parcel;
            _projectName.Text = parcel.ProjectName;
            _city.Text = parcel.City;
            _district.Text = parcel.District;
            _neighborhood.Text = parcel.Neighborhood;
            _blockNumber.Text = parcel.Block;
            _parcelNumber.Text = parcel.Parcel;
            _parcelArea.Text = TextUtil.FormatArea (parcel.ParcelArea);
            _taksRate.Text = TextUtil.FormatRate (parcel.TaksRate);
            _kaksRate.Text = TextUtil.FormatRate (parcel.KaksRate);
            _directEmsal.Text = TextUtil.FormatArea (parcel.DirectEmsal);
            _footprint.Text = TextUtil.FormatArea (parcel.BuildingFootprint);
            _commonArea.Text = TextUtil.FormatArea (_project.CommonArea);
            _parking.Text = _project.ProvidedParkingSpaces.ToString ();
            _emsalMethod.SelectedIndex = parcel.EmsalMethod == "direct" ? 1 : 0;
            UpdateEmsalMethodFields ();

            _unitGrid.Rows.Clear ();
            foreach (BlockRecord block in _project.Blocks) {
                foreach (IndependentUnit unit in block.Units) {
                    _unitGrid.Rows.Add (block.Name, unit.Number, unit.Floor, unit.Quality,
                        unit.RoomCount.ToString (),
                        TextUtil.FormatArea (unit.GrossArea), TextUtil.FormatArea (unit.ExtensionGrossArea),
                        TextUtil.FormatArea (unit.NetArea), TextUtil.FormatArea (unit.ExtensionNetArea),
                        TextUtil.FormatArea (unit.BalconyArea), unit.LandShare, unit.Owner);
                }
            }

            _floorGrid.Rows.Clear ();
            foreach (BlockRecord block in _project.Blocks) {
                foreach (FloorRecord floor in block.Floors) {
                    _floorGrid.Rows.Add (block.Name, floor.Name,
                        TextUtil.FormatArea (floor.EmsalArea), TextUtil.FormatArea (floor.EmsalOutsideArea),
                        TextUtil.FormatArea (CalculationEngine.SumValues (floor.ThirtyPercentAreas)),
                        TextUtil.FormatArea (CalculationEngine.SumValues (floor.ConstructionAreas) +
                                             block.UnitGrossOnFloor (floor.Name)));
                }
            }
        }

        private void ApplyFormToProject ()
        {
            ParcelInfo parcel = _project.Parcel;
            parcel.ProjectName = _projectName.Text.Trim ();
            parcel.City = _city.Text.Trim ();
            parcel.District = _district.Text.Trim ();
            parcel.Neighborhood = _neighborhood.Text.Trim ();
            parcel.Block = _blockNumber.Text.Trim ();
            parcel.Parcel = _parcelNumber.Text.Trim ();
            parcel.ParcelArea = TextUtil.ParseNumberLoose (_parcelArea.Text);
            parcel.TaksRate = TextUtil.ParseNumberLoose (_taksRate.Text);
            parcel.KaksRate = TextUtil.ParseNumberLoose (_kaksRate.Text);
            parcel.DirectEmsal = TextUtil.ParseNumberLoose (_directEmsal.Text);
            parcel.BuildingFootprint = TextUtil.ParseNumberLoose (_footprint.Text);
            parcel.EmsalMethod = _emsalMethod.SelectedIndex == 1 ? "direct" : "kaks";
            _project.ProvidedParkingSpaces = (int) Math.Round (TextUtil.ParseNumberLoose (_parking.Text));

            // The common area typed here replaces only the manual part; the
            // share that came from TIP=ORTAK etiketler stays with the scan.
            double commonFromCad = _project.AuxNumber ("cadCommonArea");
            _project.SetAuxNumber ("commonArea",
                Math.Max (commonFromCad, TextUtil.ParseNumberLoose (_commonArea.Text)));

            ApplyUnitGrid ();
            ApplyFloorGrid ();
            _project.SortUnits ();
            _project.SortFloors ();
        }

        /// <summary>
        /// Rows are matched to the records they came from, so the bookkeeping
        /// that lets a re-scan replace only its own contribution survives an
        /// edit in the form.
        /// </summary>
        private void ApplyUnitGrid ()
        {
            var seen = new HashSet<string> (StringComparer.Ordinal);
            foreach (DataGridViewRow row in _unitGrid.Rows) {
                if (row.IsNewRow) continue;
                string blockName = TextUtil.Normalize (CellText (row, 0));
                string number = CellText (row, 1);
                if (blockName.Length == 0 || number.Length == 0) continue;

                BlockRecord block = FindOrAddBlock (blockName);
                IndependentUnit unit = block.Units.FirstOrDefault (item => item.Number.Trim () == number);
                if (unit == null) {
                    unit = new IndependentUnit { Number = number };
                    block.Units.Add (unit);
                }
                unit.Floor = CellText (row, 2);
                unit.Quality = CellText (row, 3);
                unit.RoomCount = (int) Math.Round (TextUtil.ParseNumberLoose (CellText (row, 4)));
                unit.GrossArea = TextUtil.ParseNumberLoose (CellText (row, 5));
                unit.ExtensionGrossArea = TextUtil.ParseNumberLoose (CellText (row, 6));
                unit.NetArea = TextUtil.ParseNumberLoose (CellText (row, 7));
                unit.ExtensionNetArea = TextUtil.ParseNumberLoose (CellText (row, 8));
                unit.BalconyArea = TextUtil.ParseNumberLoose (CellText (row, 9));
                unit.LandShare = CellText (row, 10);
                unit.Owner = CellText (row, 11);
                if (unit.Floor.Length > 0) FindOrAddFloor (block, unit.Floor);
                seen.Add (blockName + "\u0001" + number);
            }

            foreach (BlockRecord block in _project.Blocks) {
                string blockName = TextUtil.Normalize (block.Name);
                block.Units.RemoveAll (unit => !seen.Contains (blockName + "\u0001" + unit.Number.Trim ()));
            }
        }

        private void ApplyFloorGrid ()
        {
            var seen = new HashSet<string> (StringComparer.Ordinal);
            foreach (DataGridViewRow row in _floorGrid.Rows) {
                if (row.IsNewRow) continue;
                string blockName = TextUtil.Normalize (CellText (row, 0));
                string floorName = CellText (row, 1);
                if (blockName.Length == 0 || floorName.Length == 0) continue;

                BlockRecord block = FindOrAddBlock (blockName);
                FloorRecord floor = FindOrAddFloor (block, floorName);
                floor.EmsalArea = TextUtil.ParseNumberLoose (CellText (row, 2));
                floor.EmsalOutsideArea = TextUtil.ParseNumberLoose (CellText (row, 3));
                seen.Add (blockName + "\u0001" + floorName);
            }

            foreach (BlockRecord block in _project.Blocks) {
                string blockName = TextUtil.Normalize (block.Name);
                block.Floors.RemoveAll (floor => !seen.Contains (blockName + "\u0001" + floor.Name));
            }
            _project.Blocks.RemoveAll (block => block.Floors.Count == 0 && block.Units.Count == 0);
        }

        private BlockRecord FindOrAddBlock (string normalizedName)
        {
            BlockRecord block = _project.FindBlock (normalizedName);
            if (block != null) return block;
            block = new BlockRecord { Name = normalizedName };
            _project.Blocks.Add (block);
            return block;
        }

        private static FloorRecord FindOrAddFloor (BlockRecord block, string floorName)
        {
            FloorRecord floor = block.FindFloor (floorName);
            if (floor != null) return floor;
            floor = new FloorRecord { Name = floorName, SortIndex = FloorOrder.Rank (floorName) };
            block.Floors.Add (floor);
            return floor;
        }

        private static string CellText (DataGridViewRow row, int column)
        {
            object value = row.Cells[column].Value;
            return value == null ? string.Empty : value.ToString ().Trim ();
        }

        private void SetStatus (string message)
        {
            CalculationSummary summary = CalculationEngine.Calculate (_project);
            string emsal = summary.MaxEmsal > 0.0
                ? (summary.EmsalOk
                    ? "bakiye " + TextUtil.FormatArea (summary.EmsalBalance) + " m²"
                    : "AŞIM " + TextUtil.FormatArea (summary.EmsalExcess) + " m²")
                : "emsal hakkı girilmedi";
            _status.ForeColor = summary.MaxEmsal > 0.0 && !summary.EmsalOk ? Color.Firebrick : SystemColors.ControlText;
            _status.Text =
                "Emsal: " + TextUtil.FormatArea (summary.CalculatedEmsal) + " / " +
                TextUtil.FormatArea (summary.MaxEmsal) + " m² (" + emsal + ")   ·   " +
                "BB: " + summary.UnitCount + "   ·   Yapı inşaat: " +
                TextUtil.FormatArea (summary.ConstructionArea) + " m²   ·   Otopark: " +
                summary.RequiredParkingSpaces + " gerekli / " + _project.ProvidedParkingSpaces + " ayrılan" +
                (string.IsNullOrEmpty (message) ? string.Empty : "\n" + message);
        }
    }
}
