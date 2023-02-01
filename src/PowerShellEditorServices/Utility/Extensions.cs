// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class ObjectExtensions
    {
        /// <summary>
        /// Extension to evaluate an object's ToString() method in an exception safe way. This will
        /// extension method will not throw.
        /// </summary>
        /// <param name="obj">The object on which to call ToString()</param>
        /// <returns>The ToString() return value or a suitable error message is that throws.</returns>
        public static string SafeToString(this object obj)
        {
            string str;

            try
            {
                str = obj.ToString();
            }
            catch (Exception ex)
            {
                str = $"<Error converting property value to string - {ex.Message}>";
            }

            return str;
        }

        /// <summary>
        /// Same as <see cref="StringBuilder.AppendLine()" /> but never CRLF. Use this when building
        /// formatting for clients that may not render CRLF correctly.
        /// </summary>
        /// <param name="self"></param>
        public static StringBuilder AppendLineLF(this StringBuilder self) => self.Append('\n');

        /// <summary>
        /// Same as <see cref="StringBuilder.AppendLine(string)" /> but never CRLF. Use this when building
        /// formatting for clients that may not render CRLF correctly.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="value"></param>
        public static StringBuilder AppendLineLF(this StringBuilder self, string value)
            => self.Append(value).Append('\n');
    }
}
