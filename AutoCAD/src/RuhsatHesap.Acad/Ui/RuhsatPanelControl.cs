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

        /// <summary>Rows the last save dropped for having no Blok.</summary>
        private int _skippedRows;

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
        private readonly DataGridView _extraGrid = NewGrid ();
        private readonly DataGridView _wallGrid = NewGrid ();
        /// <summary>
        /// The live emsal/otopark summary plus the last action's message. A
        /// read-only multiline text box rather than a Label: the line is long
        /// enough to be cut off in a docked palette, so it has to wrap, scroll
        /// and let the user select the numbers to copy them.
        /// </summary>
        private readonly TextBox _status = new TextBox {
            Dock = DockStyle.Bottom, Height = 84, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.Control, TabStop = false
        };

        /// <summary>
        /// Son taramanın uyarıları. Kept visible in the panel because a çift
        /// etiket uyarısı silently doubles a kalem, and a user who scans from
        /// this button would otherwise never see the command line it used to
        /// be printed to.
        /// </summary>
        private readonly ListBox _warnings = new ListBox {
            Dock = DockStyle.Bottom, Height = 78, Visible = false,
            HorizontalScrollbar = true, IntegralHeight = false, ForeColor = Color.Firebrick
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
            tabs.TabPages.Add (BuildSiteTab ());

            // Double-clicking a warning copies it, so a handle like <2A3> can
            // be pasted straight into RHSOR.
            _warnings.DoubleClick += (sender, args) => {
                if (_warnings.SelectedItem != null)
                    Clipboard.SetText (_warnings.SelectedItem.ToString ());
            };
            new ToolTip ().SetToolTip (_warnings, "Son taramanın uyarıları. Çift tıklayarak kopyalayabilirsiniz.");

            Controls.Add (tabs);
            Controls.Add (_warnings);
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
            bar.Controls.Add (NewButton ("Çizimi Tara", "RHTARA — etiketli alanları okur", (sender, args) => ScanDrawing ()));
            bar.Controls.Add (NewButton ("Tabloları Çiz", "RHTABLOLAR", (sender, args) => RunCommand ("RHTABLOLAR")));
            bar.Controls.Add (NewButton ("Excel", "RHEXCEL", (sender, args) => RunCommand ("RHEXCEL")));
            bar.Controls.Add (NewButton ("JSON Kaydet", "RHJSONKAYDET", (sender, args) => RunCommand ("RHJSONKAYDET")));
            return bar;
        }

        private TabPage BuildParcelTab ()
        {
            var page = new TabPage ("Parsel") { Padding = new Padding (6), AutoScroll = true };
            // Docked Top (not Fill) with AutoSize: the standard scrollable-form
            // pairing, where the panel grows to the height its rows need and
            // the page scrolls over it. Dock.Fill together with AutoSize
            // contradict each other and left the last row's label stranded.
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Top, ColumnCount = 2, RowCount = 0, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
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
                Text = "%30 istisna ve yapı inşaat kalemleri etiketlerden okunur (TIP=MERDIVEN, HESAP=EMSAL, " +
                       "TIP=BALKON …); burada toplamları görünür."
            };
            page.Controls.Add (note);
            return page;
        }

        /// <summary>
        /// Foseptik, su deposu, trafo binası and istinat duvarları belong to
        /// the parsel, not to a blok or a kat -- the Katlar grid would drop
        /// them for having no Blok. They get their own tab, matching the
        /// TIP=EK_YAPI / TIP=ISTINAT etiketler, which likewise carry neither
        /// BLOK nor KAT.
        /// </summary>
        private TabPage BuildSiteTab ()
        {
            var page = new TabPage ("Ek Yapılar") { Padding = new Padding (6) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add (new RowStyle (SizeType.Percent, 50));
            layout.RowStyles.Add (new RowStyle (SizeType.Percent, 50));

            AddGridColumn (_extraGrid, "Ad", 190);
            AddGridColumn (_extraGrid, "Alan (m²)", 90);
            layout.Controls.Add (BuildSiteGroup (
                "Ek yapılar — foseptik, su deposu, trafo … (blok ve kat gerekmez)",
                _extraGrid, "Ek yapı ekle", row => row.Cells[0].Value = "Foseptik",
                "Yapı İnşaat Alanı toplamına eklenir. Çizimden okumak için: RH|TIP=EK_YAPI|AD=Foseptik"));

            AddGridColumn (_wallGrid, "Ad", 190);
            AddGridColumn (_wallGrid, "Alan (m²)", 90);
            layout.Controls.Add (BuildSiteGroup (
                "İstinat duvarları (blok ve kat gerekmez)",
                _wallGrid, "İstinat duvarı ekle", row => row.Cells[0].Value = "İstinat Duvarı",
                "Çizimden okumak için: RH|TIP=ISTINAT|AD=Doğu İstinat"));

            page.Controls.Add (layout);
            return page;
        }

        private Control BuildSiteGroup (string title, DataGridView grid, string addText,
            Action<DataGridViewRow> initialise, string hint)
        {
            var box = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding (6, 4, 6, 4) };
            box.Controls.Add (grid);
            box.Controls.Add (BuildGridButtons (grid, addText, initialise));
            box.Controls.Add (new Label {
                Dock = DockStyle.Bottom, Height = 18, ForeColor = SystemColors.GrayText, Text = hint
            });
            return box;
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

        /// <summary>
        /// Places the label and its field in the same explicit row. Letting
        /// TableLayoutPanel assign cells implicitly drifted the pairing once
        /// the rows outgrew the (unset) RowCount, which is how "Projede
        /// ayrılan otopark" ended up far from its own text box.
        /// </summary>
        private static void AddRow (TableLayoutPanel layout, string label, Control field)
        {
            int row = layout.RowCount;
            layout.RowCount = row + 1;
            layout.RowStyles.Add (new RowStyle (SizeType.AutoSize));

            // Both sides anchored Left only, so each is centred against the
            // other in the row's height whatever the field turns out to be --
            // a text box, a combo, or a text box with a button beside it.
            layout.Controls.Add (new Label {
                Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding (0, 3, 6, 3)
            }, 0, row);
            field.Anchor = AnchorStyles.Left;
            layout.Controls.Add (field, 1, row);
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
                // A row with data but no Blok used to vanish without a word.
                // Say so, and point at the tab that does not need one.
                string skipped = _skippedRows > 0
                    ? " " + _skippedRows + " satır BLOK boş olduğu için atlandı — foseptik, su deposu " +
                      "gibi bloka ait olmayan yapılar için \"Ek Yapılar\" sekmesini kullanın."
                    : string.Empty;
                if (report) SetStatus ("Veriler çizime kaydedildi. (DWG'yi kaydetmeyi unutmayın.)" + skipped);
                else SetStatus (skipped.Length > 0 ? skipped.Trim () : null);
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
        /// Runs the çizim taraması here rather than queueing RHTARA, so the
        /// scan's warnings can be shown in the panel. Queued commands run
        /// asynchronously and write only to the command line, which is how a
        /// çift etiket uyarısı could previously go unnoticed while it doubled
        /// a kalem.
        /// </summary>
        private void ScanDrawing ()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) {
                SetStatus ("Açık çizim yok.");
                return;
            }
            try {
                SaveToDrawing (false);
                ScanOutcome outcome = ScanRunner.Run (document);
                _project = outcome.Project;
                FillFormFromProject ();
                ShowWarnings (outcome.Warnings);

                string summary = "Tarama tamam: " + outcome.Stats.Tagged + " etiketli nesne, " +
                    outcome.Result.Recognized + " alan okundu";
                SetStatus (outcome.Warnings.Count > 0
                    ? summary + " — " + outcome.Warnings.Count + " UYARI (aşağıda)."
                    : summary + ".");
            } catch (System.Exception exception) {
                SetStatus ("Tarama başarısız: " + exception.Message);
            }
        }

        private void ShowWarnings (IReadOnlyList<string> warnings)
        {
            _warnings.BeginUpdate ();
            _warnings.Items.Clear ();
            foreach (string warning in warnings) _warnings.Items.Add ("! " + warning);
            _warnings.EndUpdate ();
            _warnings.Visible = warnings.Count > 0;
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

            _extraGrid.Rows.Clear ();
            foreach (ExtraStructure structure in _project.ExtraStructures)
                _extraGrid.Rows.Add (structure.Name, TextUtil.FormatArea (structure.Area));

            _wallGrid.Rows.Clear ();
            foreach (RetainingWall wall in _project.RetainingWalls)
                _wallGrid.Rows.Add (wall.Name, TextUtil.FormatArea (wall.Area));
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

            _skippedRows = ApplyUnitGrid () + ApplyFloorGrid ();
            ApplySiteGrids ();
            _project.SortUnits ();
            _project.SortFloors ();
            _project.SortRetainingWalls ();
        }

        /// <summary>
        /// Ek yapılar and istinat duvarları are parsel-level: no blok, no kat.
        /// The grids own the whole list, so deleting a row here removes it --
        /// including one a scan created, which the next RHTARA puts back.
        /// </summary>
        private void ApplySiteGrids ()
        {
            _project.ExtraStructures.Clear ();
            foreach (DataGridViewRow row in _extraGrid.Rows) {
                if (row.IsNewRow) continue;
                string name = CellText (row, 0);
                if (name.Length == 0) continue;
                _project.ExtraStructures.Add (new ExtraStructure {
                    Name = name, Area = TextUtil.ParseNumberLoose (CellText (row, 1))
                });
            }

            _project.RetainingWalls.Clear ();
            foreach (DataGridViewRow row in _wallGrid.Rows) {
                if (row.IsNewRow) continue;
                string name = CellText (row, 0);
                if (name.Length == 0) continue;
                _project.RetainingWalls.Add (new RetainingWall {
                    Name = name, Area = TextUtil.ParseNumberLoose (CellText (row, 1))
                });
            }
        }

        /// <summary>
        /// Rows are matched to the records they came from, so the bookkeeping
        /// that lets a re-scan replace only its own contribution survives an
        /// edit in the form.
        /// </summary>
        /// <returns>Blok/BB boş olduğu için atlanan satır sayısı.</returns>
        private int ApplyUnitGrid ()
        {
            int skipped = 0;
            var seen = new HashSet<string> (StringComparer.Ordinal);
            foreach (DataGridViewRow row in _unitGrid.Rows) {
                if (row.IsNewRow) continue;
                string blockName = TextUtil.Normalize (CellText (row, 0));
                string number = CellText (row, 1);
                if (blockName.Length == 0 || number.Length == 0) {
                    if (!IsRowBlank (row)) skipped++;
                    continue;
                }

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
            return skipped;
        }

        /// <returns>Blok/kat boş olduğu için atlanan satır sayısı.</returns>
        private int ApplyFloorGrid ()
        {
            int skipped = 0;
            var seen = new HashSet<string> (StringComparer.Ordinal);
            foreach (DataGridViewRow row in _floorGrid.Rows) {
                if (row.IsNewRow) continue;
                string blockName = TextUtil.Normalize (CellText (row, 0));
                string floorName = CellText (row, 1);
                if (blockName.Length == 0 || floorName.Length == 0) {
                    if (!IsRowBlank (row)) skipped++;
                    continue;
                }

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
            return skipped;
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

        /// <summary>
        /// A row the user never filled in. Distinguishing these from a row
        /// that has data but no Blok is what makes the "atlandı" warning
        /// meaningful instead of firing on every empty trailing row.
        /// </summary>
        private static bool IsRowBlank (DataGridViewRow row)
        {
            for (int column = 0; column < row.Cells.Count; column++)
                if (CellText (row, column).Length > 0) return false;
            return true;
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
                // A multiline TextBox only breaks on a full CRLF.
                (string.IsNullOrEmpty (message) ? string.Empty : "\r\n" + message);
        }
    }
}
