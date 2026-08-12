using System;
using System.Collections.Generic;
using System.Text;

namespace RuhsatHesap.Core.Tagging
{
    /// <summary>Which calculation the measured polyline area feeds.</summary>
    public enum AreaKind
    {
        // Bağımsız bölüm alanları
        Net,
        Gross,
        ExtensionNet,
        ExtensionGross,
        Balcony,
        // Ortak / kat alanları
        Common,
        Stair,
        Hall,
        Emsal,
        EmsalOutside,
        Shelter,
        Eave,
        Elevator,
        /// <summary>Any TIP outside the reserved list. Resolved during sync
        /// against the project's own Yapı İnşaat Alanı / Emsal %30 columns.</summary>
        CustomFloorArea,
        // Parsel düzeyi kalemler, AutoCAD çiziminden doğrudan okunur
        ParcelBoundary,
        BuildingFootprint,
        RetainingWall,
        /// <summary>Site-level construction item that is not istinat duvarı --
        /// foseptik, trafo, su deposu binası vb. Adds straight into the Yapı
        /// İnşaat Alanı total; needs neither BLOK nor KAT.</summary>
        ExtraStructure,
        /// <summary>Kat sınırı çerçevesi: içine düşen etiketli polylineların
        /// katını belirler, kendi alanı hesaba girmez.</summary>
        FloorFrame,
        Unknown
    }

    /// <summary>
    /// A parsed RH etiketi. The grammar is the Archicad zone-name standard with
    /// one AutoCAD-specific addition: <c>KAT=</c>, because a DWG has no story
    /// list to read the floor from.
    /// </summary>
    public sealed class RuhsatTag
    {
        public bool IsRuhsatTag;
        public bool Valid;
        public string BlockName = string.Empty;
        public string UnitNumber = string.Empty;
        public string FloorName = string.Empty;
        public string RoomName = string.Empty;
        public string Quality = string.Empty;
        public string Label = string.Empty;
        public string AreaTypeName = string.Empty;
        public AreaKind Kind = AreaKind.Unknown;
        public int RoomCount;
        /// <summary>HESAP=EMSAL: route the area into the Emsal Hesabı %30
        /// istisna tablosu instead of Yapı İnşaat Alanı.</summary>
        public bool ToThirtyPercentTable;
        public string Error = string.Empty;

        public bool IsUnitArea => IsUnitAreaKind (Kind);
        public bool NeedsFloor => Kind != AreaKind.Common && Kind != AreaKind.ParcelBoundary &&
                                  Kind != AreaKind.BuildingFootprint && Kind != AreaKind.RetainingWall &&
                                  Kind != AreaKind.ExtraStructure;
        /// <summary>
        /// Parsel-level items belong to the whole project, and TIP=ORTAK is a
        /// project-wide total, so those carry no BLOK. Everything else is
        /// written into a block's tables and needs one.
        /// </summary>
        public bool NeedsBlock => Kind != AreaKind.ParcelBoundary && Kind != AreaKind.BuildingFootprint &&
                                  Kind != AreaKind.RetainingWall && Kind != AreaKind.FloorFrame &&
                                  Kind != AreaKind.Common && Kind != AreaKind.ExtraStructure;

        public static bool IsUnitAreaKind (AreaKind kind) =>
            kind == AreaKind.Net || kind == AreaKind.Gross || kind == AreaKind.ExtensionNet ||
            kind == AreaKind.ExtensionGross || kind == AreaKind.Balcony;

        /// <summary>Reserved TIP values, in the order the etiket form offers them.</summary>
        public static readonly string[] ReservedTypes = {
            "NET", "BRUT", "EKLENTI_NET", "EKLENTI_BRUT", "BALKON", "ORTAK",
            "MERDIVEN", "HOL", "ASANSOR", "SACAK", "SIGINAK", "EMSAL", "EMSAL_DISI",
            "PARSEL", "OTURUM", "ISTINAT", "EK_YAPI", "KAT_SINIRI"
        };

        /// <summary>TIP values that HESAP=EMSAL may redirect.</summary>
        public static bool SupportsHesapEmsal (AreaKind kind) =>
            kind == AreaKind.Stair || kind == AreaKind.Hall || kind == AreaKind.Eave ||
            kind == AreaKind.Elevator || kind == AreaKind.CustomFloorArea;

        public static AreaKind ParseAreaType (string value)
        {
            switch (TextUtil.Normalize (value)) {
                case "NET": return AreaKind.Net;
                case "BRUT":
                case "GROSS": return AreaKind.Gross;
                case "EKLENTI_NET":
                case "EKLENTINET": return AreaKind.ExtensionNet;
                case "EKLENTI_BRUT":
                case "EKLENTIBRUT": return AreaKind.ExtensionGross;
                case "BALKON": return AreaKind.Balcony;
                case "ORTAK": return AreaKind.Common;
                case "MERDIVEN": return AreaKind.Stair;
                case "HOL": return AreaKind.Hall;
                case "EMSAL": return AreaKind.Emsal;
                case "EMSAL_DISI":
                case "EMSALDISI": return AreaKind.EmsalOutside;
                case "SIGINAK": return AreaKind.Shelter;
                case "SACAK": return AreaKind.Eave;
                case "ASANSOR": return AreaKind.Elevator;
                case "PARSEL":
                case "PARSEL_SINIRI": return AreaKind.ParcelBoundary;
                case "OTURUM":
                case "TABAN":
                case "TAKS": return AreaKind.BuildingFootprint;
                case "ISTINAT":
                case "ISTINAT_DUVARI": return AreaKind.RetainingWall;
                case "EK_YAPI":
                case "EKYAPI":
                case "EKSTRA":
                case "EKSTRA_YAPI": return AreaKind.ExtraStructure;
                case "KAT_SINIRI":
                case "KATSINIRI":
                case "KAT_CERCEVESI": return AreaKind.FloorFrame;
                default: return AreaKind.Unknown;
            }
        }

        /// <summary>Fixed map key for the reserved floor-level area types.</summary>
        public static string FloorAreaKey (AreaKind kind)
        {
            switch (kind) {
                case AreaKind.Stair: return "merdiven";
                case AreaKind.Hall: return "hol";
                case AreaKind.Shelter: return "siginak";
                case AreaKind.Eave: return "sacak";
                case AreaKind.Elevator: return "asansor";
                default: return string.Empty;
            }
        }

        public const string Prefix = "RH";

        /// <summary>
        /// Parses "RH|BLOK=A|BB=01|KAT=ZEMİN KAT|TIP=NET|ODA=3". Compact
        /// aliases (B, T, O, M, N, K, H) are accepted, ':' works instead of
        /// '='. Anything not starting with RH is not a Ruhsat Hesap etiketi and
        /// is ignored by the scan.
        /// </summary>
        public static RuhsatTag Parse (string text)
        {
            var result = new RuhsatTag ();
            if (string.IsNullOrEmpty (text)) return result;

            string[] tokens = text.Split ('|');
            for (int index = 0; index < tokens.Length; index++) {
                string token = tokens[index].Trim ();
                if (index == 0) {
                    if (TextUtil.Normalize (token) != Prefix) return result;
                    result.IsRuhsatTag = true;
                    continue;
                }
                if (token.Length == 0) continue;

                int separator = token.IndexOf ('=');
                if (separator < 0) separator = token.IndexOf (':');
                if (separator < 0) continue;

                string key = TextUtil.Normalize (token.Substring (0, separator));
                string value = token.Substring (separator + 1).Trim ();
                switch (key) {
                    case "BLOK": case "B": result.BlockName = TextUtil.Normalize (value); break;
                    case "BB": case "BAGIMSIZBOLUM": result.UnitNumber = value; break;
                    case "KAT": case "K": result.FloorName = value; break;
                    case "TIP": case "T":
                        result.AreaTypeName = value;
                        result.Kind = ParseAreaType (value);
                        break;
                    case "ODA": case "O":
                        result.RoomCount = int.TryParse (value.Trim (), out int rooms) ? Math.Max (0, rooms) : 0;
                        break;
                    case "MAHAL": case "M": result.RoomName = value; break;
                    case "NITELIK": case "N": result.Quality = value; break;
                    case "AD": case "A": result.Label = value; break;
                    case "HESAP": case "H":
                        result.ToThirtyPercentTable = TextUtil.Normalize (value) == "EMSAL";
                        break;
                }
            }

            if (result.Kind == AreaKind.Unknown && result.AreaTypeName.Length == 0)
                result.Error = "TIP eksik";
            else if (result.NeedsBlock && result.BlockName.Length == 0)
                result.Error = "BLOK eksik";
            else
                result.Valid = true;
            return result;
        }

        /// <summary>Rebuilds the canonical etiket text for this tag.</summary>
        public string Format ()
        {
            var builder = new StringBuilder (Prefix);
            void Append (string key, string value)
            {
                if (!string.IsNullOrEmpty (value)) builder.Append ('|').Append (key).Append ('=').Append (value.Trim ());
            }

            Append ("BLOK", BlockName);
            Append ("BB", UnitNumber);
            Append ("KAT", FloorName);
            Append ("TIP", AreaTypeName.Length > 0 ? AreaTypeName : DefaultTypeName (Kind));
            if (RoomCount > 0) Append ("ODA", RoomCount.ToString (System.Globalization.CultureInfo.InvariantCulture));
            Append ("MAHAL", RoomName);
            Append ("NITELIK", Quality);
            Append ("AD", Label);
            if (ToThirtyPercentTable && SupportsHesapEmsal (Kind)) Append ("HESAP", "EMSAL");
            return builder.ToString ();
        }

        public static string DefaultTypeName (AreaKind kind)
        {
            switch (kind) {
                case AreaKind.Net: return "NET";
                case AreaKind.Gross: return "BRUT";
                case AreaKind.ExtensionNet: return "EKLENTI_NET";
                case AreaKind.ExtensionGross: return "EKLENTI_BRUT";
                case AreaKind.Balcony: return "BALKON";
                case AreaKind.Common: return "ORTAK";
                case AreaKind.Stair: return "MERDIVEN";
                case AreaKind.Hall: return "HOL";
                case AreaKind.Emsal: return "EMSAL";
                case AreaKind.EmsalOutside: return "EMSAL_DISI";
                case AreaKind.Shelter: return "SIGINAK";
                case AreaKind.Eave: return "SACAK";
                case AreaKind.Elevator: return "ASANSOR";
                case AreaKind.ParcelBoundary: return "PARSEL";
                case AreaKind.BuildingFootprint: return "OTURUM";
                case AreaKind.RetainingWall: return "ISTINAT";
                case AreaKind.ExtraStructure: return "EK_YAPI";
                case AreaKind.FloorFrame: return "KAT_SINIRI";
                default: return string.Empty;
            }
        }

        /// <summary>Short human-readable description used in the palette and in
        /// the polyline area table.</summary>
        public static string Describe (AreaKind kind)
        {
            switch (kind) {
                case AreaKind.Net: return "BB net alanı";
                case AreaKind.Gross: return "BB brüt alanı";
                case AreaKind.ExtensionNet: return "Eklenti net alanı";
                case AreaKind.ExtensionGross: return "Eklenti brüt alanı";
                case AreaKind.Balcony: return "Balkon alanı";
                case AreaKind.Common: return "Ortak alan";
                case AreaKind.Stair: return "Merdiven";
                case AreaKind.Hall: return "Hol";
                case AreaKind.Emsal: return "Emsal alanı";
                case AreaKind.EmsalOutside: return "Emsal dışı alan";
                case AreaKind.Shelter: return "Sığınak";
                case AreaKind.Eave: return "Saçak";
                case AreaKind.Elevator: return "Asansör";
                case AreaKind.ParcelBoundary: return "Parsel sınırı";
                case AreaKind.BuildingFootprint: return "Yapı oturum alanı";
                case AreaKind.RetainingWall: return "İstinat duvarı";
                case AreaKind.ExtraStructure: return "Ek yapı (foseptik, trafo, su deposu vb.)";
                case AreaKind.FloorFrame: return "Kat sınırı";
                case AreaKind.CustomFloorArea: return "Serbest alan kalemi";
                default: return "Tanımsız";
            }
        }

        public static IReadOnlyList<string> ReservedTypeList => ReservedTypes;
    }
}
