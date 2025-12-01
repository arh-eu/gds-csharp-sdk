using System;
using System.Globalization;

namespace GDSNetSdk.InsertQueryDemo
{
    /// <summary>
    /// Generates time-based identifiers with a configurable 4-character prefix.
    /// Format: PREFIX + YYMMDDHHmmssfff
    /// </summary>
    public class IdGenerator
    {
        private readonly string _prefix;

        public IdGenerator(string prefix)
        {
            ValidatePrefix(prefix);
            _prefix = prefix;
        }

        /// <summary>
        /// Generates an identifier from the current UTC system time.
        /// </summary>
        public string Generate()
        {
            var nowUtc = DateTime.UtcNow;
            string datetimePart = nowUtc.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
            return _prefix + datetimePart;
        }

        /// <summary>
        /// Generates an identifier from a Unix timestamp in milliseconds.
        /// </summary>
        public string GenerateFromEpochMilliseconds(long epochMilliseconds)
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds);
            var utc = dto.UtcDateTime;

            string datetimePart = utc.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
            return _prefix + datetimePart;
        }

        private static void ValidatePrefix(string prefix)
        {
            if (prefix == null)
                throw new ArgumentNullException(nameof(prefix));

            if (prefix.Length != 4)
                throw new ArgumentException("The prefix must be exactly 4 characters long.", nameof(prefix));
        }
    }
}
