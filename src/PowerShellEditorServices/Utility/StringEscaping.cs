using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class StringEscaping
    {
        public static StringBuilder SingleQuoteAndEscape(string s)
        {
            var dequotedString = s.TrimStart('\'').TrimEnd('\'');
            var psEscapedInnerQuotes = dequotedString.Replace("'", "`'");
            return new StringBuilder(s.Length)
                .Append('\'')
                .Append(psEscapedInnerQuotes)
                .Append('\'');
        }

        public static bool PowerShellArgumentNeedsEscaping(string argument)
        {
            //Already quoted arguments dont require escaping unless there is a quote inside as well
            if (argument.StartsWith("'") && argument.EndsWith("'"))
            {
                var dequotedString = argument.TrimStart('\'').TrimEnd('\'');
                // need to escape if there is a single quote between single quotes
                return dequotedString.Contains("'");
            }

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

        public static string EscapePowershellArgument(string argument)
        {
            if (PowerShellArgumentNeedsEscaping(argument))
            {
                return SingleQuoteAndEscape(argument).ToString();
            }
            else
            {
                return argument;
            }
        }

    }
}
