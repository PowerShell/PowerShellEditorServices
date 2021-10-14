using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class StringEscaping
    {
        public static StringBuilder SingleQuoteAndEscape(string s)
        {
            return new StringBuilder(s.Length)
                .Append("'")
                .Append(s.Replace("'", "''"))
                .Append("'");
        }

        public static bool PowerShellArgumentNeedsEscaping(string argument)
        {
            foreach (char c in argument)
            {
                switch (c)
                {
                    case '\'':
                    case '"':
                    case '|':
                    case '&':
                    case ';':
                    case ':':
                    case char w when char.IsWhiteSpace(w):
                        return true;
                }
            }

            return false;
        }
    }
}
