using System;
using System.Text;

namespace RuhsatHesap.Core.Tagging
{
    /// <summary>
    /// Etiket text conversions that do not need AutoCAD: the layer-name
    /// convention and the annotation-text fallback. Kept in the core so the
    /// grammar stays covered by tests.
    /// </summary>
    public static class TagText
    {
        /// <summary>
        /// Converts an "RH-BLOK_A-BB_01-TIP_NET" layer name into the canonical
        /// etiket text. AutoCAD layer names cannot contain '|' or '=', so the
        /// layer convention uses '-' between parts and the FIRST '_' of each
        /// part as the key/value separator — which keeps TIP_EKLENTI_NET
        /// unambiguous.
        /// </summary>
        public static bool TryParseLayerName (string layerName, out string tagText)
        {
            tagText = string.Empty;
            if (string.IsNullOrEmpty (layerName)) return false;
            string[] parts = layerName.Split ('-');
            if (parts.Length < 2 || TextUtil.Normalize (parts[0]) != RuhsatTag.Prefix) return false;

            var builder = new StringBuilder (RuhsatTag.Prefix);
            bool hasPair = false;
            for (int index = 1; index < parts.Length; index++) {
                string part = parts[index].Trim ();
                int separator = part.IndexOf ('_');
                if (separator <= 0 || separator == part.Length - 1) continue;
                builder.Append ('|')
                       .Append (part.Substring (0, separator))
                       .Append ('=')
                       .Append (part.Substring (separator + 1));
                hasPair = true;
            }
            if (!hasPair) return false;
            tagText = builder.ToString ();
            return true;
        }

        /// <summary>True when a piece of annotation text is an RH etiketi.</summary>
        public static bool LooksLikeTag (string text)
        {
            if (string.IsNullOrEmpty (text)) return false;
            string trimmed = text.TrimStart ();
            if (trimmed.Length < 3) return false;
            if (TextUtil.Normalize (trimmed.Substring (0, 2)) != RuhsatTag.Prefix) return false;
            return trimmed[2] == '|' || trimmed[2] == ' ';
        }

        /// <summary>
        /// Strips MTEXT formatting codes so an etiket typed inside formatted
        /// MTEXT is still readable.
        /// </summary>
        public static string StripMTextFormatting (string text)
        {
            if (string.IsNullOrEmpty (text)) return string.Empty;
            var builder = new StringBuilder (text.Length);
            for (int index = 0; index < text.Length; index++) {
                char character = text[index];
                if (character == '\\' && index + 1 < text.Length) {
                    char next = text[index + 1];
                    if (next == 'P' || next == 'p') { builder.Append (' '); index++; continue; }
                    if (next == '\\' || next == '{' || next == '}') { builder.Append (next); index++; continue; }
                    // Formatting run such as \fArial|b0|i0; -- skip to its
                    // terminating semicolon so the run never reaches the parser.
                    int terminator = text.IndexOf (';', index);
                    if (terminator > 0) { index = terminator; continue; }
                    index++;
                    continue;
                }
                if (character == '{' || character == '}') continue;
                builder.Append (character);
            }
            return builder.ToString ().Trim ();
        }
    }
}
