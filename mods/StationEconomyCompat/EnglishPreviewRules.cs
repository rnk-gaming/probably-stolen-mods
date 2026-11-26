using System;

namespace StationEconomyCompat
{
    internal static class EnglishPreviewRules
    {
        internal const string ReplyMarker = "[\u5f85\u56de\u590d] ";

        internal static bool Chinese(char c) => c >= '\u3400' && c <= '\u9fff';
        internal static bool HasChinese(string text)
        {
            foreach (char c in text) if (Chinese(c)) return true;
            return false;
        }

        internal static string Cut(string english, string shownMarker, float width, Func<string, float> measure)
        {
            if (measure(shownMarker + english) <= width) return english;
            int low = 0, high = english.Length;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (BetweenPair(english, middle)) middle--;
                // The next possible boundary can be two UTF-16 units away.
                if (middle == low) middle += 2;
                if (measure(shownMarker + english.Substring(0, middle) + "\u2026") <= width) low = middle;
                else
                {
                    high = middle - 1;
                    if (BetweenPair(english, high)) high--;
                }
            }
            return english.Substring(0, low).TrimEnd() + "\u2026";
        }

        private static bool BetweenPair(string text, int index) => index > 0 && index < text.Length
            && char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]);
    }
}
