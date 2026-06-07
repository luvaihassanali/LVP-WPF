using System.Globalization;

namespace LVP_WPF.Util
{
    /// <summary>
    /// String fix-ups for TMDB-fetched text. TMDB returns UTF-8; somewhere
    /// along the line it's getting double-decoded and producing mojibake
    /// (`â€™` instead of `'`, `Ã©` instead of `é`, etc.) plus a handful of
    /// other punctuation oddities the player doesn't render cleanly.
    /// The proper fix is upstream (read responses as UTF-8 from the start);
    /// until then this patches at the consumer.
    /// </summary>
    public static class StringExtensions
    {
        private const string targetSingleQuoteSymbol = "'";
        private const string genericSingleQuoteSymbol = "â€™";
        private const string openSingleQuoteSymbol = "â€˜";
        private const string closeSingleQuoteSymbol = "â€™";
        private const string frenchAccentAigu = "Ã©";
        private const string frenchAccentGrave = "Ã";

        public static string FixBrokenQuotes(this string str)
        {
            return str.Replace(genericSingleQuoteSymbol, targetSingleQuoteSymbol)
                .Replace(openSingleQuoteSymbol, targetSingleQuoteSymbol)
                .Replace(closeSingleQuoteSymbol, targetSingleQuoteSymbol)
                .Replace(frenchAccentAigu, "e")
                .Replace(frenchAccentGrave, "a")
                .Replace("%", "percent")
                .Replace("  ", " ");
        }

        /// <summary>
        /// Case- and symbol-insensitive equality used by the MediaEnricher
        /// and CustomCache when matching local filenames to TMDB titles.
        /// Wraps the verbose
        /// <c>String.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols) == 0</c>
        /// boilerplate that used to appear inline.
        /// </summary>
        public static bool MatchesLoosely(this string a, string b)
            => string.Compare(a, b, CultureInfo.CurrentCulture,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols) == 0;
    }
}
