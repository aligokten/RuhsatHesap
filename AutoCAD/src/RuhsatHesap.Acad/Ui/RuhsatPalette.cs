using System;
using Autodesk.AutoCAD.Windows;

namespace RuhsatHesap.Acad.Ui
{
    /// <summary>
    /// Hosts <see cref="RuhsatPanelControl"/> in an AutoCAD palette: dockable,
    /// resizable, and remembered between sessions through its fixed GUID. This
    /// is the AutoCAD counterpart of the Archicad add-on's palette.
    /// </summary>
    public static class RuhsatPalette
    {
        private static readonly Guid PaletteId = new Guid ("6C1F4A62-9D74-4F0E-9E3B-7A2C4D915E22");

        private static PaletteSet _paletteSet;
        private static RuhsatPanelControl _panel;

        public static bool IsVisible => _paletteSet != null && _paletteSet.Visible;

        /// <summary>Shows the palette, creating it on first use.</summary>
        public static void Show ()
        {
            EnsureCreated ();
            _paletteSet.Visible = true;
            _panel.LoadFromDrawing ();
        }

        public static void Hide ()
        {
            if (_paletteSet != null) _paletteSet.Visible = false;
        }

        /// <summary>Shows the palette when hidden, hides it when shown.</summary>
        public static bool Toggle ()
        {
            if (IsVisible) {
                Hide ();
                return false;
            }
            Show ();
            return true;
        }

        /// <summary>Reloads the form from the active drawing, if it is open.</summary>
        public static void Refresh ()
        {
            if (IsVisible) _panel.LoadFromDrawing ();
        }

        private static void EnsureCreated ()
        {
            if (_paletteSet != null) return;

            _panel = new RuhsatPanelControl ();
            _paletteSet = new PaletteSet ("Ruhsat Hesap", PaletteId) {
                Style = PaletteSetStyles.ShowPropertiesMenu |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.Snappable,
                MinimumSize = new System.Drawing.Size (420, 320),
                Size = new System.Drawing.Size (560, 620),
                DockEnabled = DockSides.Left | DockSides.Right
            };
            _paletteSet.Add ("Ruhsat Hesap", _panel);
        }
    }
}
