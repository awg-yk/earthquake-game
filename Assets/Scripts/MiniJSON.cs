// MiniJSON.cs - minimal JSON parser used because JsonUtility cannot
// deserialize dictionaries (intensities are keyed by prefecture name).
// Public domain style minimal JSON decoder, trimmed down to what this
// project needs: decoding into Dictionary<string, object> / List<object>.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace EarthquakeGame
{
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            char c = json[index];
            switch (c)
            {
                case '{': return ParseObject(json, ref index);
                case '[': return ParseArray(json, ref index);
                case '"': return ParseString(json, ref index);
                default:
                    if (c == 't') { index += 4; return true; }
                    if (c == 'f') { index += 5; return false; }
                    if (c == 'n') { index += 4; return null; }
                    return ParseNumber(json, ref index);
            }
        }

        static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, object>();
            index++; // '{'
            SkipWhitespace(json, ref index);
            if (json[index] == '}') { index++; return dict; }
            while (true)
            {
                SkipWhitespace(json, ref index);
                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                index++; // ':'
                object value = ParseValue(json, ref index);
                dict[key] = value;
                SkipWhitespace(json, ref index);
                char c = json[index];
                index++;
                if (c == '}') break;
                // otherwise ',' -> continue loop
            }
            return dict;
        }

        static List<object> ParseArray(string json, ref int index)
        {
            var list = new List<object>();
            index++; // '['
            SkipWhitespace(json, ref index);
            if (json[index] == ']') { index++; return list; }
            while (true)
            {
                object value = ParseValue(json, ref index);
                list.Add(value);
                SkipWhitespace(json, ref index);
                char c = json[index];
                index++;
                if (c == ']') break;
            }
            return list;
        }

        static string ParseString(string json, ref int index)
        {
            var sb = new StringBuilder();
            index++; // opening quote
            while (json[index] != '"')
            {
                char c = json[index];
                if (c == '\\')
                {
                    index++;
                    char esc = json[index];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            string hex = json.Substring(index + 1, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            index += 4;
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                index++;
            }
            index++; // closing quote
            return sb.ToString();
        }

        static object ParseNumber(string json, ref int index)
        {
            int start = index;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '-' || json[index] == '+' || json[index] == '.' || json[index] == 'e' || json[index] == 'E'))
            {
                index++;
            }
            string number = json.Substring(start, index - start);
            if (number.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0)
            {
                return double.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
            }
            return long.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
