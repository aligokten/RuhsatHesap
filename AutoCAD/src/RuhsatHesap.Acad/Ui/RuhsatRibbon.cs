using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace RuhsatHesap.Acad.Ui
{
    /// <summary>
    /// The "Ruhsat Hesap" sekmesi on AutoCAD's ribbon. It is built in code
    /// rather than shipped as a .cuix: a partial CUIx has to be authored and
    /// loaded per profile from inside AutoCAD, while a tab added through the
    /// ribbon API appears the moment the plug-in loads and needs nothing in
    /// the user's profile. The icons are drawn at run time for the same
    /// reason -- no image files to install, and nothing to go missing.
    /// Everything here is best effort: a ribbon problem must never keep the
    /// commands themselves from loading.
    /// </summary>
    public static class RuhsatRibbon
    {
        private const string TabId = "RUHSATHESAP_TAB";
        private const string IconFont = "Segoe MDL2 Assets";

        private static readonly ICommand Handler = new CommandHandler ();
        private static bool _waitingForRibbon;
        private static bool _workspaceHooked;
        private static bool _idleQueued;
        private static bool? _iconFontAvailable;

        /// <summary>
        /// Adds the tab if it is not there. Safe to call any number of times
        /// and before the ribbon exists; in that case it waits for it.
        /// </summary>
        public static void Ensure (bool activate = false)
        {
            try {
                RibbonControl ribbon = ComponentManager.Ribbon;
                if (ribbon == null) {
                    if (!_waitingForRibbon) {
                        _waitingForRibbon = true;
                        ComponentManager.ItemInitialized += OnItemInitialized;
                    }
                    return;
                }

                RibbonTab tab = ribbon.FindTab (TabId);
                if (tab == null) {
                    tab = BuildTab ();
                    ribbon.Tabs.Add (tab);
                }
                if (activate) tab.IsActive = true;
                HookWorkspaceChanges ();
            } catch (System.Exception) {
                // No ribbon (RIBBONCLOSE, a headless session, an API change):
                // the RH komutları still work from the command line.
            }
        }

        /// <summary>Ensure, but once AutoCAD is idle -- at start-up and right
        /// after a workspace switch the ribbon is still being rebuilt.</summary>
        public static void EnsureWhenIdle ()
        {
            if (_idleQueued) return;
            _idleQueued = true;
            AcadApp.Idle += OnIdle;
        }

        private static void OnIdle (object sender, EventArgs args)
        {
            AcadApp.Idle -= OnIdle;
            _idleQueued = false;
            Ensure ();
        }

        private static void OnItemInitialized (object sender, RibbonItemEventArgs args)
        {
            if (ComponentManager.Ribbon == null) return;
            ComponentManager.ItemInitialized -= OnItemInitialized;
            _waitingForRibbon = false;
            Ensure ();
        }

        /// <summary>
        /// Switching workspace (WSCURRENT) reloads the ribbon from the CUI
        /// files and drops every tab that was added in code, so it has to be
        /// put back.
        /// </summary>
        private static void HookWorkspaceChanges ()
        {
            if (_workspaceHooked) return;
            _workspaceHooked = true;
            AcadApp.SystemVariableChanged += (sender, args) => {
                if (string.Equals (args.Name, "WSCURRENT", StringComparison.OrdinalIgnoreCase))
                    EnsureWhenIdle ();
            };
        }

        private static RibbonTab BuildTab ()
        {
            var tab = new RibbonTab { Title = "Ruhsat Hesap", Id = TabId };

            Color blue = Color.FromRgb (0x1F, 0x6F, 0xB5);
            Color green = Color.FromRgb (0x2E, 0x8B, 0x57);
            Color orange = Color.FromRgb (0xC9, 0x6A, 0x12);
            Color teal = Color.FromRgb (0x12, 0x8C, 0x9C);
            Color purple = Color.FromRgb (0x6B, 0x4F, 0xA8);
            Color grey = Color.FromRgb (0x5A, 0x64, 0x70);

            tab.Panels.Add (Panel ("Proje",
                Button ("Form", "RHPANEL", "", "F", blue,
                    "Parsel, bağımsız bölüm, kat, ek yapı ve depo verilerinin girildiği formu açar/kapatır."),
                Button ("Parsel", "RHPARSEL", "", "P", blue,
                    "Parsel, ada, TAKS/KAKS bilgilerini komut satırından girer."),
                Button ("Ayarlar", "RHAYAR", "", "A", grey,
                    "Çizim birimi, aktif blok/kat, tablo yazı yüksekliği ve yazı tipi.")));

            tab.Panels.Add (Panel ("Etiket",
                Button ("Etiketle", "RHETIKET", "", "E", green,
                    "Seçilen kapalı alanlara RH etiketi yazar."),
                Button ("Sorgula", "RHSOR", "", "?", green,
                    "Bir nesnenin etiketini, tipini ve alanını gösterir."),
                Button ("Etiket Sil", "RHETIKETSIL", "", "X", grey,
                    "Seçilen nesnelerin RH etiketini siler."),
                Button ("Çizimi\nTara", "RHTARA", "", "T", green,
                    "Çizimi tarar ve etiketli alanları proje verisine işler.")));

            tab.Panels.Add (Panel ("Tablolar",
                Button ("Tüm\nTablolar", "RHTABLOLAR", "", "Σ", orange,
                    "Bütün ruhsat tablolarını alt alta çizer."),
                Button ("Emsal", "RHEMSAL", "", "E", orange,
                    "Emsal hesap tablosu, %30 istisna sütunları ve emsal kontrolü."),
                Button ("Bağımsız\nBölüm", "RHBB", "", "BB", orange,
                    "Bağımsız bölüm alan tablosu."),
                Button ("İnşaat\nAlanı", "RHINSAAT", "", "İ", orange,
                    "Yapı inşaat alanı tablosu."),
                Button ("Otopark\nAğaç", "RHOTOPARKAGAC", "", "O", orange,
                    "Otopark ve ağaç hesabı, açıklamalı."),
                Button ("Su\nDepoları", "RHSUDEPOSU", "", "S", teal,
                    "Kullanma suyu, yangın suyu ve yağmur suyu deposu hesabı, açıklamalı."),
                Button ("Alan\nTablosu", "RHALANTABLO", "", "m²", teal,
                    "Seçilen polylineların alan hesap tablosu (etiketsizler dâhil)."),
                Button ("Tabloları\nTemizle", "RHTABLOTEMIZLE", "", "X", grey,
                    "RH-TABLO katmanındaki eski tabloları siler.")));

            tab.Panels.Add (Panel ("Aktarım",
                Button ("Excel", "RHEXCEL", "", "XL", purple,
                    "Bütün tabloları formatlı .xlsx çalışma kitabına yazar."),
                Button ("JSON\nKaydet", "RHJSONKAYDET", "", "↓", purple,
                    "Proje verisini .json olarak kaydeder."),
                Button ("JSON\nAç", "RHJSONAC", "", "↑", purple,
                    "Web paneli / Archicad .json dosyasını okur.")));

            tab.Panels.Add (Panel ("Yardım",
                Button ("Komutlar", "RHYARDIM", "", "?", grey,
                    "Bütün RH komutlarını komut satırına yazar.")));
            return tab;
        }

        private static RibbonPanel Panel (string title, params RibbonButton[] buttons)
        {
            var source = new RibbonPanelSource { Title = title };
            foreach (RibbonButton button in buttons) source.Items.Add (button);
            return new RibbonPanel { Source = source };
        }

        private static RibbonButton Button (string text, string command, string glyph, string fallback,
            Color color, string description)
        {
            return new RibbonButton {
                Id = "RH_RIBBON_" + command,
                Text = text,
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                LargeImage = Icon (32, glyph, fallback, color),
                Image = Icon (16, glyph, fallback, color),
                ToolTip = description + "  (" + command + ")",
                CommandParameter = command,
                CommandHandler = Handler
            };
        }

        /// <summary>
        /// A rounded colour tile with a white symbol. The symbol comes from
        /// Segoe MDL2 Assets, the icon font Windows 10 and 11 ship; on a
        /// system without it a short letter is drawn instead, so a button is
        /// never an empty square.
        /// </summary>
        private static ImageSource Icon (int size, string glyph, string fallback, Color color)
        {
            bool iconFont = IconFontAvailable ();
            var visual = new DrawingVisual ();
            using (DrawingContext context = visual.RenderOpen ()) {
                double radius = size * 0.18;
                context.DrawRoundedRectangle (new SolidColorBrush (color), null,
                    new Rect (0, 0, size, size), radius, radius);

                string symbol = iconFont ? glyph : fallback;
                double emSize = iconFont ? size * 0.62 : size * (fallback.Length > 1 ? 0.42 : 0.56);
                var typeface = iconFont
                    ? new Typeface (IconFont)
                    : new Typeface (new FontFamily ("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                var text = new FormattedText (symbol, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, emSize, Brushes.White, 1.0);
                context.DrawText (text, new Point ((size - text.Width) / 2.0, (size - text.Height) / 2.0));
            }

            var bitmap = new RenderTargetBitmap (size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render (visual);
            bitmap.Freeze ();
            return bitmap;
        }

        private static bool IconFontAvailable ()
        {
            if (_iconFontAvailable == null) {
                try {
                    _iconFontAvailable = Fonts.SystemFontFamilies.Any (family =>
                        string.Equals (family.Source, IconFont, StringComparison.OrdinalIgnoreCase));
                } catch (System.Exception) {
                    _iconFontAvailable = false;
                }
            }
            return _iconFontAvailable.Value;
        }

        /// <summary>Runs the komut a ribbon button carries, cancelling any
        /// command that is still active first, as a menu macro's ^C^C does.</summary>
        private sealed class CommandHandler : ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }

            public bool CanExecute (object parameter) => true;

            public void Execute (object parameter)
            {
                var button = parameter as RibbonButton;
                string command = button?.CommandParameter as string;
                if (string.IsNullOrEmpty (command)) return;
                Document document = AcadApp.DocumentManager.MdiActiveDocument;
                if (document == null) return;
                document.SendStringToExecute ("\u0003\u0003" + command + " ", true, false, true);
            }
        }
    }
}
