using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using RuhsatHesap.Acad.Ui;

namespace RuhsatHesap.Acad.Commands
{
    /// <summary>Form penceresi (palet) komutları.</summary>
    public sealed class PanelCommands
    {
        /// <summary>
        /// Opens the Ruhsat Hesap form. Running it again hides the palette, the
        /// same way Archicad's palette command works.
        /// </summary>
        [CommandMethod ("RHPANEL", CommandFlags.Modal | CommandFlags.Session)]
        public void TogglePanel ()
        {
            Document document = AcadUi.ActiveDocument;
            Editor editor = document?.Editor;

            try {
                bool visible = RuhsatPalette.Toggle ();
                if (editor == null) return;
                AcadUi.Write (editor, visible
                    ? "Ruhsat Hesap formu açıldı. Verileri doldurup Kaydet'e basın."
                    : "Ruhsat Hesap formu gizlendi. Yeniden açmak için RHPANEL.");
            } catch (System.Exception exception) {
                if (editor != null) AcadUi.Write (editor, "Form açılamadı: " + exception.Message);
            }
        }

        /// <summary>
        /// Puts the Ruhsat Hesap sekmesi back on the ribbon and switches to
        /// it -- for when the ribbon was closed while the plug-in loaded, or
        /// a CUI reload dropped the tab.
        /// </summary>
        [CommandMethod ("RHRIBBON", CommandFlags.Modal | CommandFlags.Session)]
        public void ShowRibbon ()
        {
            RuhsatRibbon.Ensure (true);
            Document document = AcadUi.ActiveDocument;
            if (document != null)
                AcadUi.Write (document.Editor, "Ruhsat Hesap sekmesi üst şeride eklendi. " +
                    "Şerit kapalıysa önce RIBBON komutuyla açın.");
        }

        /// <summary>Reloads the form from the drawing without toggling it.</summary>
        [CommandMethod ("RHPANELYENILE", CommandFlags.Modal | CommandFlags.Session)]
        public void RefreshPanel ()
        {
            RuhsatPalette.Refresh ();
            Document document = AcadUi.ActiveDocument;
            if (document != null) AcadUi.Write (document.Editor, "Form çizimdeki veriyle yenilendi.");
        }
    }
}
