using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuhsatHesap.Core.Json
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// Minimal dependency-free JSON tree. The AutoCAD plug-in runs inside
    /// AutoCAD's own AppDomain, where pulling in Newtonsoft.Json or
    /// System.Text.Json risks colliding with a copy AutoCAD itself loaded, so
    /// the project file format is read and written here instead.
    /// Object members keep their insertion order, which makes the produced
    /// files diff-friendly and stable across saves.
    /// </summary>
    public sealed class JsonValue
    {
        private readonly List<KeyValuePair<string, JsonValue>> _members;
        private readonly Dictionary<string, int> _memberIndex;
        private readonly List<JsonValue> _items;

        public JsonKind Kind { get; }
        public bool BoolValue { get; }
        public double NumberValue { get; }
        public string StringValue { get; }

        private JsonValue (JsonKind kind, bool boolValue, double numberValue, string stringValue)
        {
            Kind = kind;
            BoolValue = boolValue;
            NumberValue = numberValue;
            StringValue = stringValue;
            if (kind == JsonKind.Object) {
                _members = new List<KeyValuePair<string, JsonValue>> ();
                _memberIndex = new Dictionary<string, int> (StringComparer.Ordinal);
            } else if (kind == JsonKind.Array) {
                _items = new List<JsonValue> ();
            }
        }

        public static readonly JsonValue Null = new JsonValue (JsonKind.Null, false, 0.0, null);

        public static JsonValue Bool (bool value) => new JsonValue (JsonKind.Bool, value, value ? 1.0 : 0.0, null);
        public static JsonValue Number (double value) => new JsonValue (JsonKind.Number, false, value, null);
        public static JsonValue String (string value) => new JsonValue (JsonKind.String, false, 0.0, value ?? string.Empty);
        public static JsonValue NewObject () => new JsonValue (JsonKind.Object, false, 0.0, null);
        public static JsonValue NewArray () => new JsonValue (JsonKind.Array, false, 0.0, null);

        public bool IsNull => Kind == JsonKind.Null;
        public bool IsObject => Kind == JsonKind.Object;
        public bool IsArray => Kind == JsonKind.Array;
        public bool IsNumber => Kind == JsonKind.Number;
        public bool IsString => Kind == JsonKind.String;

        public IReadOnlyList<KeyValuePair<string, JsonValue>> Members =>
            (IReadOnlyList<KeyValuePair<string, JsonValue>>) _members ?? Array.Empty<KeyValuePair<string, JsonValue>> ();

        public IReadOnlyList<JsonValue> Items =>
            (IReadOnlyList<JsonValue>) _items ?? Array.Empty<JsonValue> ();

        public int Count => _items != null ? _items.Count : (_members != null ? _members.Count : 0);

        public bool Has (string key) => _memberIndex != null && key != null && _memberIndex.ContainsKey (key);

        /// <summary>Object member access. Reading a missing key yields <see cref="Null"/>.</summary>
        public JsonValue this[string key]
        {
            get
            {
                if (_memberIndex != null && key != null && _memberIndex.TryGetValue (key, out int index))
                    return _members[index].Value;
                return Null;
            }
            set
            {
                if (_members == null) throw new InvalidOperationException ("JSON nesnesi değil.");
                JsonValue stored = value ?? Null;
                if (_memberIndex.TryGetValue (key, out int index))
                    _members[index] = new KeyValuePair<string, JsonValue> (key, stored);
                else {
                    _memberIndex[key] = _members.Count;
                    _members.Add (new KeyValuePair<string, JsonValue> (key, stored));
                }
            }
        }

        public JsonValue this[int index] => _items != null && index >= 0 && index < _items.Count ? _items[index] : Null;

        public void Add (JsonValue value)
        {
            if (_items == null) throw new InvalidOperationException ("JSON dizisi değil.");
            _items.Add (value ?? Null);
        }

        public void Remove (string key)
        {
            if (_members == null || key == null || !_memberIndex.TryGetValue (key, out int index)) return;
            _members.RemoveAt (index);
            _memberIndex.Remove (key);
            for (int position = index; position < _members.Count; position++)
                _memberIndex[_members[position].Key] = position;
        }

        public string AsString (string fallback = "")
        {
            switch (Kind) {
                case JsonKind.String: return StringValue;
                case JsonKind.Number: return FormatNumber (NumberValue);
                case JsonKind.Bool: return BoolValue ? "true" : "false";
                default: return fallback;
            }
        }

        /// <summary>
        /// Reads a number tolerantly: the web panel stores some fields as text
        /// and Turkish input may arrive as "1.234,56".
        /// </summary>
        public double AsDouble (double fallback = 0.0)
        {
            if (Kind == JsonKind.Number) return NumberValue;
            if (Kind == JsonKind.Bool) return BoolValue ? 1.0 : 0.0;
            if (Kind == JsonKind.String) return TextUtil.ParseNumberLoose (StringValue, fallback);
            return fallback;
        }

        public int AsInt (int fallback = 0)
        {
            double value = AsDouble (fallback);
            if (double.IsNaN (value) || double.IsInfinity (value)) return fallback;
            return (int) Math.Round (value, MidpointRounding.AwayFromZero);
        }

        public bool AsBool (bool fallback = false)
        {
            switch (Kind) {
                case JsonKind.Bool: return BoolValue;
                case JsonKind.Number: return Math.Abs (NumberValue) > double.Epsilon;
                case JsonKind.String:
                    string text = StringValue.Trim ();
                    if (text.Equals ("true", StringComparison.OrdinalIgnoreCase)) return true;
                    if (text.Equals ("false", StringComparison.OrdinalIgnoreCase)) return false;
                    return fallback;
                default: return fallback;
            }
        }

        public static string FormatNumber (double value)
        {
            if (double.IsNaN (value) || double.IsInfinity (value)) return "0";
            if (Math.Abs (value - Math.Round (value)) < 1e-9 && Math.Abs (value) < 1e15)
                return ((long) Math.Round (value)).ToString (CultureInfo.InvariantCulture);
            return value.ToString ("R", CultureInfo.InvariantCulture);
        }

        public override string ToString () => ToJson (false);

        public string ToJson (bool indented)
        {
            var builder = new StringBuilder ();
            Write (builder, indented, 0);
            return builder.ToString ();
        }

        private void Write (StringBuilder builder, bool indented, int depth)
        {
            switch (Kind) {
                case JsonKind.Null: builder.Append ("null"); return;
                case JsonKind.Bool: builder.Append (BoolValue ? "true" : "false"); return;
                case JsonKind.Number: builder.Append (FormatNumber (NumberValue)); return;
                case JsonKind.String: WriteString (builder, StringValue); return;
                case JsonKind.Array:
                    if (_items.Count == 0) { builder.Append ("[]"); return; }
                    builder.Append ('[');
                    for (int index = 0; index < _items.Count; index++) {
                        if (index > 0) builder.Append (',');
                        NewLine (builder, indented, depth + 1);
                        _items[index].Write (builder, indented, depth + 1);
                    }
                    NewLine (builder, indented, depth);
                    builder.Append (']');
                    return;
                default:
                    if (_members.Count == 0) { builder.Append ("{}"); return; }
                    builder.Append ('{');
                    for (int index = 0; index < _members.Count; index++) {
                        if (index > 0) builder.Append (',');
                        NewLine (builder, indented, depth + 1);
                        WriteString (builder, _members[index].Key);
                        builder.Append (':');
                        if (indented) builder.Append (' ');
                        _members[index].Value.Write (builder, indented, depth + 1);
                    }
                    NewLine (builder, indented, depth);
                    builder.Append ('}');
                    return;
            }
        }

        private static void NewLine (StringBuilder builder, bool indented, int depth)
        {
            if (!indented) return;
            builder.Append ('\n');
            builder.Append (' ', depth * 2);
        }

        private static void WriteString (StringBuilder builder, string value)
        {
            builder.Append ('"');
            foreach (char character in value ?? string.Empty) {
                switch (character) {
                    case '"': builder.Append ("\\\""); break;
                    case '\\': builder.Append ("\\\\"); break;
                    case '\b': builder.Append ("\\b"); break;
                    case '\f': builder.Append ("\\f"); break;
                    case '\n': builder.Append ("\\n"); break;
                    case '\r': builder.Append ("\\r"); break;
                    case '\t': builder.Append ("\\t"); break;
                    default:
                        if (character < ' ') builder.Append ("\\u").Append (((int) character).ToString ("x4", CultureInfo.InvariantCulture));
                        else builder.Append (character);
                        break;
                }
            }
            builder.Append ('"');
        }

        public static JsonValue Parse (string text)
        {
            if (text == null) throw new FormatException ("JSON metni boş.");
            int position = 0;
            SkipWhitespace (text, ref position);
            // Byte order mark shows up when a project file was saved by Excel
            // or Notepad; skip it rather than failing the whole import.
            if (position < text.Length && text[position] == '\uFEFF') position++;
            JsonValue value = ParseValue (text, ref position);
            SkipWhitespace (text, ref position);
            if (position < text.Length) throw new FormatException ("JSON sonunda beklenmeyen karakter: " + text[position]);
            return value;
        }

        public static bool TryParse (string text, out JsonValue value)
        {
            try {
                value = Parse (text);
                return true;
            } catch (FormatException) {
                value = Null;
                return false;
            }
        }

        private static JsonValue ParseValue (string text, ref int position)
        {
            SkipWhitespace (text, ref position);
            if (position >= text.Length) throw new FormatException ("JSON beklenmedik şekilde bitti.");
            char character = text[position];
            switch (character) {
                case '{': return ParseObject (text, ref position);
                case '[': return ParseArray (text, ref position);
                case '"': return String (ParseString (text, ref position));
                case 't': Expect (text, ref position, "true"); return Bool (true);
                case 'f': Expect (text, ref position, "false"); return Bool (false);
                case 'n': Expect (text, ref position, "null"); return Null;
                default: return Number (ParseNumber (text, ref position));
            }
        }

        private static JsonValue ParseObject (string text, ref int position)
        {
            JsonValue result = NewObject ();
            position++; // '{'
            SkipWhitespace (text, ref position);
            if (position < text.Length && text[position] == '}') { position++; return result; }
            while (true) {
                SkipWhitespace (text, ref position);
                if (position >= text.Length || text[position] != '"') throw new FormatException ("JSON nesnesinde anahtar bekleniyordu.");
                string key = ParseString (text, ref position);
                SkipWhitespace (text, ref position);
                if (position >= text.Length || text[position] != ':') throw new FormatException ("JSON nesnesinde ':' bekleniyordu.");
                position++;
                result[key] = ParseValue (text, ref position);
                SkipWhitespace (text, ref position);
                if (position >= text.Length) throw new FormatException ("JSON nesnesi kapatılmadı.");
                if (text[position] == ',') { position++; continue; }
                if (text[position] == '}') { position++; return result; }
                throw new FormatException ("JSON nesnesinde ',' veya '}' bekleniyordu.");
            }
        }

        private static JsonValue ParseArray (string text, ref int position)
        {
            JsonValue result = NewArray ();
            position++; // '['
            SkipWhitespace (text, ref position);
            if (position < text.Length && text[position] == ']') { position++; return result; }
            while (true) {
                result.Add (ParseValue (text, ref position));
                SkipWhitespace (text, ref position);
                if (position >= text.Length) throw new FormatException ("JSON dizisi kapatılmadı.");
                if (text[position] == ',') { position++; continue; }
                if (text[position] == ']') { position++; return result; }
                throw new FormatException ("JSON dizisinde ',' veya ']' bekleniyordu.");
            }
        }

        private static string ParseString (string text, ref int position)
        {
            position++; // opening quote
            var builder = new StringBuilder ();
            while (true) {
                if (position >= text.Length) throw new FormatException ("JSON metni kapatılmadı.");
                char character = text[position++];
                if (character == '"') return builder.ToString ();
                if (character != '\\') { builder.Append (character); continue; }
                if (position >= text.Length) throw new FormatException ("JSON kaçış dizisi eksik.");
                char escape = text[position++];
                switch (escape) {
                    case '"': builder.Append ('"'); break;
                    case '\\': builder.Append ('\\'); break;
                    case '/': builder.Append ('/'); break;
                    case 'b': builder.Append ('\b'); break;
                    case 'f': builder.Append ('\f'); break;
                    case 'n': builder.Append ('\n'); break;
                    case 'r': builder.Append ('\r'); break;
                    case 't': builder.Append ('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length) throw new FormatException ("JSON \\u kaçış dizisi eksik.");
                        builder.Append ((char) int.Parse (text.Substring (position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        position += 4;
                        break;
                    default: throw new FormatException ("Bilinmeyen JSON kaçış dizisi: \\" + escape);
                }
            }
        }

        private static double ParseNumber (string text, ref int position)
        {
            int start = position;
            if (position < text.Length && (text[position] == '-' || text[position] == '+')) position++;
            while (position < text.Length && (char.IsDigit (text[position]) || text[position] == '.' ||
                   text[position] == 'e' || text[position] == 'E' || text[position] == '-' || text[position] == '+'))
                position++;
            string slice = text.Substring (start, position - start);
            if (double.TryParse (slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return value;
            throw new FormatException ("Geçersiz JSON sayısı: " + slice);
        }

        private static void Expect (string text, ref int position, string literal)
        {
            if (position + literal.Length > text.Length || string.CompareOrdinal (text, position, literal, 0, literal.Length) != 0)
                throw new FormatException ("Geçersiz JSON sabiti, beklenen: " + literal);
            position += literal.Length;
        }

        private static void SkipWhitespace (string text, ref int position)
        {
            while (position < text.Length && char.IsWhiteSpace (text[position])) position++;
        }
    }
}
