using System;
using System.Globalization;
using System.Text;

namespace ShopkeepingSuite
{
    // A bounded JSON reader for one top-level number. It never executes Module Forge code.
    // All other values are still validated, so a nested key or malformed tail cannot pass.
    internal sealed class CommissionText
    {
        private readonly string text;
        private int at;
        private int? reputation;
        private CommissionText(string text) { this.text = text; }
        private static void Require(bool valid) { if (!valid) throw new FormatException("unreadable commission standing"); }
        internal static int Reputation(string text)
        {
            Require(text != null && text.Length <= 2097152);
            var reader = new CommissionText(text);
            reader.Object(0); reader.Space();
            Require(reader.at == text.Length && reader.reputation.HasValue);
            return reader.reputation.Value;
        }
        private void Space() { while (at < text.Length && (text[at] == ' ' || text[at] == '\t' || text[at] == '\r' || text[at] == '\n')) at++; }
        private bool Take(char c) { Space(); if (at >= text.Length || text[at] != c) return false; at++; return true; }
        private void Expect(char c) => Require(Take(c));
        private void Object(int depth)
        {
            Require(depth <= 64); Expect('{');
            if (Take('}')) return;
            do
            {
                string key = String(); Expect(':');
                if (depth == 0 && key == "Reputation")
                {
                    Require(!reputation.HasValue);
                    string number = Number();
                    Require(int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value));
                    reputation = value;
                }
                else Value(depth + 1);
                if (Take('}')) return;
                Expect(',');
            } while (true);
        }
        private void Value(int depth)
        {
            Space(); Require(depth <= 64 && at < text.Length);
            switch (text[at])
            {
                case '{': Object(depth); return;
                case '[':
                    at++; if (Take(']')) return;
                    do { Value(depth + 1); if (Take(']')) return; Expect(','); } while (true);
                case '"': String(); return;
                case 't': Literal("true"); return;
                case 'f': Literal("false"); return;
                case 'n': Literal("null"); return;
                default: Number(); return;
            }
        }
        private void Literal(string value)
        {
            Require(at + value.Length <= text.Length && string.CompareOrdinal(text, at, value, 0, value.Length) == 0);
            at += value.Length;
        }
        private static bool Digit(char c) => c >= '0' && c <= '9';
        private string Number()
        {
            Space(); int start = at;
            if (at < text.Length && text[at] == '-') at++;
            Require(at < text.Length && Digit(text[at]));
            if (text[at] == '0') at++;
            else while (at < text.Length && Digit(text[at])) at++;
            if (at < text.Length && text[at] == '.') { at++; Digits(); }
            if (at < text.Length && (text[at] == 'e' || text[at] == 'E'))
            {
                at++; if (at < text.Length && (text[at] == '+' || text[at] == '-')) at++;
                Digits();
            }
            return text.Substring(start, at - start);
        }
        private void Digits()
        {
            int start = at; while (at < text.Length && Digit(text[at])) at++;
            Require(at > start);
        }
        private string String()
        {
            Expect('"'); var result = new StringBuilder();
            while (at < text.Length)
            {
                char c = text[at++];
                if (c == '"') return result.ToString();
                Require(c >= ' ');
                if (c == '\\')
                {
                    Require(at < text.Length); c = text[at++];
                    switch (c)
                    {
                        case '"': case '\\': case '/': break;
                        case 'b': c = '\b'; break;
                        case 'f': c = '\f'; break;
                        case 'n': c = '\n'; break;
                        case 'r': c = '\r'; break;
                        case 't': c = '\t'; break;
                        case 'u':
                            Require(at + 4 <= text.Length && ushort.TryParse(text.Substring(at, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _));
                            c = (char)ushort.Parse(text.Substring(at, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture); at += 4; break;
                        default: throw new FormatException("unreadable commission standing");
                    }
                }
                result.Append(c);
            }
            throw new FormatException("unreadable commission standing");
        }
    }
}
