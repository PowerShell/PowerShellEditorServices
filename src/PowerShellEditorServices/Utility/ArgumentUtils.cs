// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class ArgumentEscaping
    {
        /// <summary>
        /// Escape a PowerShell argument while still making it able to be evaluated in AddScript.
        ///
        /// NOTE: This does not "sanitize" parameters, e.g., a pipe in one argument might affect another argument.
        /// This is intentional to give flexibility to specifying arguments.
        /// It also does not try to fix invalid PowerShell syntax, e.g., a single quote in a string literal.
        /// </summary>
        public static string Escape(string Arg)
        {
            // if argument is a scriptblock return as-is
            if (Arg.StartsWith("{") && Arg.EndsWith("}"))
            {
                return Arg;
            }

            // If argument has a space enclose it in quotes unless it is already quoted
            if (Arg.Contains(" "))
            {
                if (Arg.StartsWith("\"") && Arg.EndsWith("\"") || Arg.StartsWith("'") && Arg.EndsWith("'"))
                {
                    return Arg;
                }

                return "\"" + Arg + "\"";
            }

            return Arg;
        }
    }
}
