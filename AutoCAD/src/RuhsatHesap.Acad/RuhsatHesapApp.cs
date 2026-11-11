using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using RuhsatHesap.Acad;
using RuhsatHesap.Acad.Commands;

[assembly: ExtensionApplication (typeof (RuhsatHesapApp))]
[assembly: CommandClass (typeof (SettingsCommands))]
[assembly: CommandClass (typeof (TaggingCommands))]
[assembly: CommandClass (typeof (TableCommands))]
[assembly: CommandClass (typeof (ExportCommands))]
[assembly: CommandClass (typeof (PanelCommands))]

namespace RuhsatHesap.Acad
{
    /// <summary>
    /// AutoCAD entry point. NETLOAD (or the ApplicationPlugins bundle) calls
    /// Initialize once per session; the plug-in itself keeps no global state,
    /// every setting and every project value lives in the drawing.
    /// </summary>
    public sealed class RuhsatHesapApp : IExtensionApplication
    {
        public const string Version = "0.7.0";

        public void Initialize ()
        {
            // The ribbon sekmesi first, and independently of whether a
            // drawing is open: AutoCAD can start on the Start tab with no
            // document, and the tab should be there all the same.
            Ui.RuhsatRibbon.EnsureWhenIdle ();

            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            document.Editor.WriteMessage (
                "\nRuhsat Hesap AutoCAD eklentisi " + Version +
                " yüklendi. Üst şeritte \"Ruhsat Hesap\" sekmesi; form için RHPANEL, komut listesi için RHYARDIM\n");
        }

        public void Terminate ()
        {
        }
    }
}
