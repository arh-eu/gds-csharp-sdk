using System;
using System.Collections.Generic;
using System.Text;

namespace Gds.Utils
{
    /// <summary>
    /// Utility class
    /// </summary>
    public sealed class Utils
    {
        /// <summary>
        /// Ensures the given parameter is not null, throwing exception otherwise.
        /// </summary>
        /// <typeparam name="T">type of the object</typeparam>
        /// <param name="o">The object to be checked</param>
        /// <param name="message">Error message on null value</param>
        /// <returns>The object specified</returns>
        public static T RequireNonNull<T>(T o, string message)
        {
            if(o==null)
            {
                throw new ArgumentNullException(message);
            }
            return o;
        }
    }
}
