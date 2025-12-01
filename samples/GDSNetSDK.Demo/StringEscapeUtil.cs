namespace GDSNetSdk.InsertQueryDemo
{
    /// <summary>
    /// Simple string escape helper for embedding values into SQL literals.
    /// Escapes single quotes (') and backslashes (\).
    /// </summary>
    public static class StringEscapeUtil
    {
        private static readonly string Quote = "'";
        private static readonly string QuoteReplace = "''";
        private static readonly string Backslash = "\\";
        private static readonly string BackslashReplace = "\\\\";

        public static string Escape(string value)
        {
            return value == null
                ? null
                : value.Replace(Quote, QuoteReplace)
                       .Replace(Backslash, BackslashReplace);
        }
    }
}
