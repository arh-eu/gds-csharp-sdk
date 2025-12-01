using System;
using System.Text;

namespace GDSNetSdk.InsertQueryDemo
{
    /// <summary>
    /// Utility for converting strings to lowercase hexadecimal representation (UTF-8 based).
    /// </summary>
    public static class HexUtil
    {
        public static string ToHexString(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
