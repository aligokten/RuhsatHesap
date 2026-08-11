using System;
using System.Globalization;

namespace RuhsatHesap.Core.Tagging
{
    /// <summary>
    /// A DWG has no Kat Ayarları list, so floors are ordered from their names.
    /// "2. BODRUM" sits below "BODRUM", "ZEMİN" is 0, "3. KAT" is above
    /// "1. KAT", and çatı/teras go on top. Unrecognised names keep a neutral
    /// rank and stay in the order they were first seen.
    /// </summary>
    public static class FloorOrder
    {
        public const int UnknownRank = 500;

        public static int Rank (string floorName)
        {
            string normalized = TextUtil.Normalize (floorName);
            if (normalized.Length == 0) return UnknownRank;

            int number = LeadingNumber (normalized);
            if (normalized.Contains ("BODRUM")) return -(100 + Math.Max (1, number));
            if (normalized.Contains ("SUBASMAN") || normalized.Contains ("TEMEL")) return -200;
            if (normalized.Contains ("ZEMIN")) return normalized.Contains ("ASMA") ? 5 : 0;
            if (normalized.Contains ("ASMA")) return 5;
            if (normalized.Contains ("CATI") || normalized.Contains ("TERAS")) return 1000;
            if (number > 0) return number * 10;
            return UnknownRank;
        }

        /// <summary>Reads the first integer in the text: "2. BODRUM" -> 2.</summary>
        private static int LeadingNumber (string normalized)
        {
            int index = 0;
            while (index < normalized.Length && !char.IsDigit (normalized[index])) index++;
            int start = index;
            while (index < normalized.Length && char.IsDigit (normalized[index])) index++;
            if (index == start) return 0;
            return int.TryParse (normalized.Substring (start, index - start), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
