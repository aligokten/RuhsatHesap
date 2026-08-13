using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuhsatHesap.Core
{
    /// <summary>
    /// Text helpers shared by the tag parser, the project sync and the report
    /// builders. These mirror the Archicad add-on's ZoneSync.cpp helpers so an
    /// etiket typed in AutoCAD and a zone name typed in Archicad resolve to the
    /// same project column.
    /// </summary>
    public static class TextUtil
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Trim (string value) => (value ?? string.Empty).Trim ();

        /// <summary>
        /// ASCII-folds Turkish characters and uppercases. Used for keyword
        /// comparison (BLOK, TIP, NET ...), never for values shown to the user.
        /// </summary>
        public static string Normalize (string value)
        {
            if (string.IsNullOrEmpty (value)) return string.Empty;
            var builder = new StringBuilder (value.Length);
            foreach (char character in value.Trim ()) {
                switch (character) {
                    case 'ı': case 'İ': case 'i': builder.Append ('I'); break;
                    case 'ş': case 'Ş': builder.Append ('S'); break;
                    case 'ğ': case 'Ğ': builder.Append ('G'); break;
                    case 'ü': case 'Ü': builder.Append ('U'); break;
                    case 'ö': case 'Ö': builder.Append ('O'); break;
                    case 'ç': case 'Ç': builder.Append ('C'); break;
                    default: builder.Append (char.ToUpperInvariant (character)); break;
                }
            }
            return builder.ToString ();
        }

        /// <summary>
        /// Folds a value down to a comparison code: ASCII, uppercase, and every
        /// run of non-alphanumeric characters collapsed to a single underscore.
        /// Used only to decide whether two spellings mean the same area column,
        /// never as the stored key.
        /// </summary>
        public static string NormalizeAreaCode (string value)
        {
            string normalized = Normalize (value);
            var builder = new StringBuilder (normalized.Length);
            bool separatorPending = false;
            foreach (char character in normalized) {
                if (char.IsLetterOrDigit (character)) {
                    if (separatorPending && builder.Length > 0) builder.Append ('_');
                    builder.Append (character);
                    separatorPending = false;
                } else {
                    separatorPending = builder.Length > 0;
                }
            }
            return builder.ToString ();
        }

        /// <summary>
        /// Derives a NEW area-column key from free-form text, e.g.
        /// "Havuz Kenarı" -> "havuz_kenarı". Mirrors the web panel's
        /// normalizeAreaKey: lowercase, whitespace runs collapsed to an
        /// underscore, punctuation and Turkish characters preserved.
        /// </summary>
        public static string NormalizeAreaKey (string value)
        {
            string text = Trim (value);
            var builder = new StringBuilder (text.Length);
            bool pendingUnderscore = false;
            foreach (char character in text) {
                if (char.IsWhiteSpace (character)) {
                    if (builder.Length > 0) pendingUnderscore = true;
                    continue;
                }
                if (pendingUnderscore) { builder.Append ('_'); pendingUnderscore = false; }
                builder.Append (character >= 'A' && character <= 'Z' ? (char) (character - 'A' + 'a') : ToLowerTurkish (character));
            }
            return builder.ToString ();
        }

        private static char ToLowerTurkish (char character)
        {
            switch (character) {
                case 'İ': return 'i';
                case 'I': return 'ı';
                case 'Ş': return 'ş';
                case 'Ğ': return 'ğ';
                case 'Ü': return 'ü';
                case 'Ö': return 'ö';
                case 'Ç': return 'ç';
                default: return character;
            }
        }

        /// <summary>
        /// Parses a number written either in invariant form ("12.5") or in
        /// Turkish form ("1.234,56" / "12,5").
        /// </summary>
        public static double ParseNumberLoose (string value, double fallback = 0.0)
        {
            string text = Trim (value);
            if (text.Length == 0) return fallback;
            text = text.Replace (" ", string.Empty).Replace (" ", string.Empty);
            if (text.IndexOf (',') >= 0) {
                // Turkish notation: '.' groups thousands, ',' is the decimal mark.
                text = text.Replace (".", string.Empty).Replace (',', '.');
            }
            return double.TryParse (text, NumberStyles.Float, Invariant, out double parsed) ? parsed : fallback;
        }

        /// <summary>Formats an area the Turkish way: 1234.5 -> "1.234,50".</summary>
        public static string FormatArea (double value, int decimals = 2)
        {
            if (double.IsNaN (value) || double.IsInfinity (value)) value = 0.0;
            return value.ToString ("N" + decimals.ToString (Invariant), CultureInfo.GetCultureInfo ("tr-TR"));
        }

        public static string FormatRate (double value)
        {
            if (double.IsNaN (value) || double.IsInfinity (value)) value = 0.0;
            return value.ToString ("0.####", Invariant).Replace ('.', ',');
        }

        /// <summary>
        /// Human/natural ordering for independent-unit numbers, so 7, 8, 9, 10
        /// is preferred over the lexical 10, 7, 8, 9. Port of
        /// ProjectData.cpp's NaturalUnitNumberLess.
        /// </summary>
        public static int CompareUnitNumbers (string left, string right)
        {
            left = left ?? string.Empty;
            right = right ?? string.Empty;
            int leftIndex = 0, leftEnd = left.Length, rightIndex = 0, rightEnd = right.Length;
            while (leftIndex < leftEnd && char.IsWhiteSpace (left[leftIndex])) leftIndex++;
            while (leftEnd > leftIndex && char.IsWhiteSpace (left[leftEnd - 1])) leftEnd--;
            while (rightIndex < rightEnd && char.IsWhiteSpace (right[rightIndex])) rightIndex++;
            while (rightEnd > rightIndex && char.IsWhiteSpace (right[rightEnd - 1])) rightEnd--;

            int leadingZeroTieBreak = 0;
            while (leftIndex < leftEnd && rightIndex < rightEnd) {
                bool leftDigit = left[leftIndex] >= '0' && left[leftIndex] <= '9';
                bool rightDigit = right[rightIndex] >= '0' && right[rightIndex] <= '9';

                if (leftDigit && rightDigit) {
                    int leftRunEnd = leftIndex, rightRunEnd = rightIndex;
                    while (leftRunEnd < leftEnd && left[leftRunEnd] >= '0' && left[leftRunEnd] <= '9') leftRunEnd++;
                    while (rightRunEnd < rightEnd && right[rightRunEnd] >= '0' && right[rightRunEnd] <= '9') rightRunEnd++;

                    int leftSignificant = leftIndex, rightSignificant = rightIndex;
                    while (leftSignificant < leftRunEnd && left[leftSignificant] == '0') leftSignificant++;
                    while (rightSignificant < rightRunEnd && right[rightSignificant] == '0') rightSignificant++;

                    int leftDigits = leftRunEnd - leftSignificant;
                    int rightDigits = rightRunEnd - rightSignificant;
                    if (leftDigits != rightDigits) return leftDigits < rightDigits ? -1 : 1;
                    for (int offset = 0; offset < leftDigits; offset++) {
                        char leftCharacter = left[leftSignificant + offset];
                        char rightCharacter = right[rightSignificant + offset];
                        if (leftCharacter != rightCharacter) return leftCharacter < rightCharacter ? -1 : 1;
                    }

                    if (leadingZeroTieBreak == 0 && (leftRunEnd - leftIndex) != (rightRunEnd - rightIndex))
                        leadingZeroTieBreak = (leftRunEnd - leftIndex) < (rightRunEnd - rightIndex) ? -1 : 1;
                    leftIndex = leftRunEnd;
                    rightIndex = rightRunEnd;
                    continue;
                }

                if (leftDigit != rightDigit) return leftDigit ? -1 : 1;
                char leftUpper = char.ToUpperInvariant (left[leftIndex]);
                char rightUpper = char.ToUpperInvariant (right[rightIndex]);
                if (leftUpper != rightUpper) return leftUpper < rightUpper ? -1 : 1;
                leftIndex++;
                rightIndex++;
            }

            if (leftIndex != leftEnd || rightIndex != rightEnd) return leftIndex == leftEnd ? -1 : 1;
            if (leadingZeroTieBreak != 0) return leadingZeroTieBreak;
            return string.CompareOrdinal (left, right);
        }

        public sealed class UnitNumberComparer : IComparer<string>
        {
            public static readonly UnitNumberComparer Instance = new UnitNumberComparer ();
            public int Compare (string left, string right) => CompareUnitNumbers (left, right);
        }

        /// <summary>
        /// Natural ordering for free-form names -- istinat duvarları, ek
        /// yapılar and anything else the user types a name for. Same algorithm
        /// as <see cref="CompareUnitNumbers"/>, which is not specific to unit
        /// numbers: it compares digit runs numerically, so "İstinat Duvarı 2"
        /// sorts before "İstinat Duvarı 10" rather than after it, and folds
        /// case without depending on the machine's culture.
        /// </summary>
        public sealed class NaturalNameComparer : IComparer<string>
        {
            public static readonly NaturalNameComparer Instance = new NaturalNameComparer ();
            public int Compare (string left, string right) => CompareUnitNumbers (left, right);
        }

        /// <summary>
        /// Folds a floor name down to a comparison key: same normalization as
        /// <see cref="Normalize"/>, with every whitespace character removed
        /// entirely rather than just collapsed. Spacing around a kat name
        /// carries no meaning, but its presence or absence is exactly the kind
        /// of typo that splits one floor into two: "1.KAT", "1. Kat" and
        /// "1 . KAT" must all resolve to the same row, and a missing space is
        /// not a "run of whitespace" that collapsing alone would catch.
        /// </summary>
        public static string NormalizeFloorKey (string value)
        {
            string normalized = Normalize (value);
            var builder = new StringBuilder (normalized.Length);
            foreach (char character in normalized)
                if (!char.IsWhiteSpace (character)) builder.Append (character);
            return builder.ToString ();
        }

        /// <summary>
        /// Turns an area-column key into the label used in reports. Mirrors
        /// ReportExport.cpp's FriendlyName so the AutoCAD tables, the Excel
        /// workbook and the Archicad add-on all title the same column
        /// identically.
        /// </summary>
        public static string FriendlyName (string key)
        {
            if (string.IsNullOrEmpty (key)) return string.Empty;
            switch (key) {
                case "merdiven": return "Merdiven";
                case "acik_cikma": case "açık_çıkma": return "Açık Çıkma";
                case "sacak": case "saçak": return "Saçak";
                case "havuz": return "Havuz";
                case "asansor": case "asansör": return "Asansör";
                case "kat_holu": return "Emsale Konu Kat Holü";
                case "hol": return "Toplam Kat Holü";
                case "giris_terasi": case "giriş_terası": return "Giriş Terası";
                case "bosluklar": case "boşluklar": return "Boşluklar";
                case "makina_odasi": return "Makina Odası";
                case "enerji_odasi": return "Enerji Odası";
                case "su_deposu": return "Su Deposu";
                case "siginak": case "sığınak": return "Sığınak";
                case "haberlesme_odasi": return "Haberleşme Odası";
                case "bagimsiz_bolum_brut": return "Bağımsız Bölüm Brüt Alanı";
            }
            string label = key.Replace ('_', ' ');
            return char.ToUpperInvariant (label[0]) + label.Substring (1);
        }
    }
}
