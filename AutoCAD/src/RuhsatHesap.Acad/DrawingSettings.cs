using System;
using System.Collections.Generic;
using RuhsatHesap.Core;
using RuhsatHesap.Core.Json;

namespace RuhsatHesap.Acad
{
    /// <summary>
    /// Drawing unit the plan is drawn in. Every measured area is multiplied by
    /// <see cref="AreaFactor"/> to reach square metres, which is what every
    /// Ruhsat Hesap table expects.
    /// </summary>
    public enum DrawingUnit
    {
        Meter,
        Centimeter,
        Millimeter
    }

    public static class DrawingUnitInfo
    {
        public static double AreaFactor (DrawingUnit unit)
        {
            switch (unit) {
                case DrawingUnit.Centimeter: return 1.0 / 10000.0;
                case DrawingUnit.Millimeter: return 1.0 / 1000000.0;
                default: return 1.0;
            }
        }

        /// <summary>Default table text height for the unit: 2,5 mm on paper at 1/100.</summary>
        public static double DefaultTextHeight (DrawingUnit unit)
        {
            switch (unit) {
                case DrawingUnit.Centimeter: return 25.0;
                case DrawingUnit.Millimeter: return 250.0;
                default: return 0.25;
            }
        }

        public static string Label (DrawingUnit unit)
        {
            switch (unit) {
                case DrawingUnit.Centimeter: return "santimetre";
                case DrawingUnit.Millimeter: return "milimetre";
                default: return "metre";
            }
        }

        public static string Keyword (DrawingUnit unit)
        {
            switch (unit) {
                case DrawingUnit.Centimeter: return "Santimetre";
                case DrawingUnit.Millimeter: return "Milimetre";
                default: return "Metre";
            }
        }

        public static bool TryParse (string keyword, out DrawingUnit unit)
        {
            switch (TextUtil.Normalize (keyword)) {
                case "METRE": case "M": unit = DrawingUnit.Meter; return true;
                case "SANTIMETRE": case "CM": unit = DrawingUnit.Centimeter; return true;
                case "MILIMETRE": case "MM": unit = DrawingUnit.Millimeter; return true;
                default: unit = DrawingUnit.Meter; return false;
            }
        }
    }

    /// <summary>
    /// Per-drawing plug-in settings, stored in the drawing next to the project
    /// data so a DWG carries its own unit and active floor.
    /// </summary>
    public sealed class DrawingSettings
    {
        public DrawingUnit Unit = DrawingUnit.Centimeter;
        /// <summary>Floor used for etiketler that carry no KAT= value.</summary>
        public string ActiveFloor = string.Empty;
        /// <summary>Block used as the default when tagging.</summary>
        public string ActiveBlock = "A";
        /// <summary>Text height of the tables drawn into model space.</summary>
        public double TableTextHeight;
        /// <summary>Text style the tables are written with. Created from the
        /// matching font when the drawing does not have it yet.</summary>
        public string TableTextStyle = "ISOCPEUR";
        /// <summary>Whether RHETIKET also draws a visible label.</summary>
        public bool WriteLabels = true;
        public string LabelLayer = "RH-ETIKET";

        public double AreaFactor => DrawingUnitInfo.AreaFactor (Unit);

        public double EffectiveTextHeight =>
            TableTextHeight > 0.0 ? TableTextHeight : DrawingUnitInfo.DefaultTextHeight (Unit);

        public string ToJson ()
        {
            JsonValue root = JsonValue.NewObject ();
            root["unit"] = JsonValue.String (Unit.ToString ());
            root["activeFloor"] = JsonValue.String (ActiveFloor);
            root["activeBlock"] = JsonValue.String (ActiveBlock);
            root["tableTextHeight"] = JsonValue.Number (TableTextHeight);
            root["tableTextStyle"] = JsonValue.String (TableTextStyle);
            root["writeLabels"] = JsonValue.Bool (WriteLabels);
            root["labelLayer"] = JsonValue.String (LabelLayer);
            return root.ToJson (false);
        }

        public static DrawingSettings FromJson (string text)
        {
            var settings = new DrawingSettings ();
            if (string.IsNullOrEmpty (text) || !JsonValue.TryParse (text, out JsonValue root) || !root.IsObject)
                return settings;
            string unit = root["unit"].AsString ();
            foreach (DrawingUnit candidate in new[] { DrawingUnit.Meter, DrawingUnit.Centimeter, DrawingUnit.Millimeter })
                if (string.Equals (unit, candidate.ToString (), StringComparison.OrdinalIgnoreCase)) settings.Unit = candidate;
            settings.ActiveFloor = root["activeFloor"].AsString ();
            settings.ActiveBlock = root["activeBlock"].AsString ("A");
            if (settings.ActiveBlock.Length == 0) settings.ActiveBlock = "A";
            settings.TableTextHeight = root["tableTextHeight"].AsDouble ();
            string textStyle = root["tableTextStyle"].AsString ();
            if (textStyle.Length > 0) settings.TableTextStyle = textStyle;
            settings.WriteLabels = root["writeLabels"].AsBool (true);
            string layer = root["labelLayer"].AsString ();
            if (layer.Length > 0) settings.LabelLayer = layer;
            return settings;
        }

        /// <summary>
        /// Guesses the unit from the drawing's INSUNITS value, used the first
        /// time a drawing is scanned.
        /// </summary>
        public static DrawingUnit FromInsUnits (int insUnits)
        {
            switch (insUnits) {
                case 4: return DrawingUnit.Millimeter;   // UnitsValue.Millimeters
                case 5: return DrawingUnit.Centimeter;   // UnitsValue.Centimeters
                case 6: return DrawingUnit.Meter;        // UnitsValue.Meters
                default: return DrawingUnit.Centimeter;  // Türkiye'de en yaygın çizim birimi
            }
        }

        public static IReadOnlyList<DrawingUnit> All =>
            new[] { DrawingUnit.Meter, DrawingUnit.Centimeter, DrawingUnit.Millimeter };
    }
}
