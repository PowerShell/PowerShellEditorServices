// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class FormatUtils
    {
        private const char GenericOpen = '[';

        private const char GenericClose = ']';

        private const string Static = "static ";

        /// <summary>
        /// Space, new line, carriage return and tab.
        /// </summary>
        private static readonly ReadOnlyMemory<char> s_whiteSpace = new[] { '\n', '\r', '\t', ' ' };

        /// <summary>
        /// A period, comma, and both open and square brackets.
        /// </summary>
        private static readonly ReadOnlyMemory<char> s_commaSquareBracketOrDot = new[] { '.', ',', '[', ']' };

        internal static string? GetTypeDocumentation(ILogger logger, string? toolTip, out MarkupKind kind)
        {
            if (toolTip is null)
            {
                kind = default;
                return null;
            }

            try
            {
                kind = MarkupKind.Markdown;
                StringBuilder text = new();
                HashSet<string>? usingNamespaces = null;

                text.Append('[');
                ProcessType(toolTip.AsSpan(), text, ref usingNamespaces);
                text.AppendLineLF("]").Append("```");
                return PrependUsingStatements(text, usingNamespaces)
                    .Insert(0, "```powershell\n")
                    .ToString();
            }
            catch (Exception e)
            {
                logger.LogHandledException($"Failed to type property tool tip \"{toolTip}\".", e);
                kind = MarkupKind.PlainText;
                return toolTip.Replace("\r\n", "\n\n");
            }
        }

        internal static string? GetPropertyDocumentation(ILogger logger, string? toolTip, out MarkupKind kind)
        {
            if (toolTip is null)
            {
                kind = default;
                return null;
            }

            try
            {
                return GetPropertyDocumentation(
                    StripAssemblyQualifications(toolTip).AsSpan(),
                    out kind);
            }
            catch (Exception e)
            {
                logger.LogHandledException($"Failed to parse property tool tip \"{toolTip}\".", e);
                kind = MarkupKind.PlainText;
                return toolTip.Replace("\r\n", "\n\n");
            }
        }

        internal static string? GetMethodDocumentation(ILogger logger, string? toolTip, out MarkupKind kind)
        {
            if (toolTip is null)
            {
                kind = default;
                return null;
            }

            try
            {
                return GetMethodDocumentation(
                    StripAssemblyQualifications(toolTip).AsSpan(),
                    out kind);
            }
            catch (Exception e)
            {
                logger.LogHandledException($"Failed to parse method tool tip \"{toolTip}\".", e);
                kind = MarkupKind.PlainText;
                return toolTip.Replace("\r\n", "\n\n");
            }
        }

        private static string GetPropertyDocumentation(ReadOnlySpan<char> toolTip, out MarkupKind kind)
        {
            kind = MarkupKind.Markdown;
            ReadOnlySpan<char> originalToolTip = toolTip;
            HashSet<string>? usingNamespaces = null;
            StringBuilder text = new();

            if (toolTip.IndexOf(Static.AsSpan(), StringComparison.Ordinal) is 0)
            {
                text.Append(Static);
                toolTip = toolTip.Slice(Static.Length);
            }

            int endOfTypeIndex = toolTip.IndexOf(' ');

            // Abort trying to process if we come across something we don't understand.
            if (endOfTypeIndex is -1)
            {
                kind = MarkupKind.PlainText;
                // Replace CRLF with LF as some clients like vim render the CR as a printable
                // character. Also double up on new lines as VSCode ignores single new lines.
                return originalToolTip.ToString().Replace("\r\n", "\n\n");
            }

            text.Append('[');
            ProcessType(toolTip.Slice(0, endOfTypeIndex), text, ref usingNamespaces);
            text.Append("] ");

            toolTip = toolTip.Slice(endOfTypeIndex + 1);

            string nameAndAccessors = toolTip.ToString();

            // Turn `{get;set;}` into `{ get; set; }` because it looks pretty. Also with namespaces
            // separated we don't need to worry as much about space. This only needs to be done
            // sometimes as for some reason instance properties already have spaces.
            if (toolTip.IndexOf("{ ".AsSpan()) is -1)
            {
                nameAndAccessors = nameAndAccessors
                    .Replace("get;", " get;")
                    .Replace("set;", " set;")
                    .Replace("}", " }");
            }

            // Add a $ so it looks like a PowerShell class property. Though we don't have the accessor
            // syntax used here, it still parses fine in the markdown.
            text.Append('$')
                .AppendLineLF(nameAndAccessors)
                .Append("```");

            return PrependUsingStatements(text, usingNamespaces)
                .Insert(0, "```powershell\n")
                .ToString();
        }

        private static string GetMethodDocumentation(ReadOnlySpan<char> toolTip, out MarkupKind kind)
        {
            kind = MarkupKind.Markdown;
            StringBuilder text = new();
            HashSet<string>? usingNamespaces = null;
            while (true)
            {
                toolTip = toolTip.TrimStart(s_whiteSpace.Span);
                toolTip = ProcessMethod(toolTip, text, ref usingNamespaces);
                if (toolTip.IsEmpty)
                {
                    return PrependUsingStatements(text.AppendLineLF().AppendLineLF("```"), usingNamespaces)
                        .Insert(0, "```powershell\n")
                        .ToString();
                }

                text.AppendLineLF().AppendLineLF();
            }
        }

        private static StringBuilder PrependUsingStatements(StringBuilder text, HashSet<string>? usingNamespaces)
        {
            if (usingNamespaces is null or { Count: 0 } || (usingNamespaces.Count is 1 && usingNamespaces.First() is "System"))
            {
                return text;
            }

            string[] namespaces = usingNamespaces.ToArray();
            Array.Sort(namespaces);
            text.Insert(0, "\n");
            for (int i = namespaces.Length - 1; i >= 0; i--)
            {
                if (namespaces[i] is "System")
                {
                    continue;
                }

                text.Insert(0, "using namespace " + namespaces[i] + "\n");
            }

            return text;
        }

        private static string StripAssemblyQualifications(string value)
        {
            // Sometimes tooltip will have fully assembly qualified names, typically when a pointer
            // is involved. This strips out the assembly qualification.
            return Regex.Replace(
                value,
                ", [a-zA-Z.]+, Version=[0-9.]+, Culture=[a-zA-Z]*, PublicKeyToken=[0-9a-fnul]* ",
                " ");
        }

        private static ReadOnlySpan<char> ProcessMethod(
            ReadOnlySpan<char> toolTip,
            StringBuilder text,
            ref HashSet<string>? usingNamespaces)
        {
            if (toolTip.IsEmpty)
            {
                return default;
            }

            if (toolTip.IndexOf(Static.AsSpan(), StringComparison.Ordinal) is 0)
            {
                text.Append(Static);
                toolTip = toolTip.Slice(Static.Length);
            }

            int endReturnTypeIndex = toolTip.IndexOf(' ');
            if (endReturnTypeIndex is -1)
            {
                text.Append(toolTip.ToString());
                return default;
            }

            text.Append('[');
            ProcessType(toolTip.Slice(0, endReturnTypeIndex), text, ref usingNamespaces);
            toolTip = toolTip.Slice(endReturnTypeIndex + 1);
            text.Append("] ");
            int endMethodNameIndex = toolTip.IndexOf('(');
            if (endMethodNameIndex is -1)
            {
                text.Append(toolTip.ToString());
                return default;
            }

            text.Append(toolTip.Slice(0, endMethodNameIndex + 1).ToString());
            toolTip = toolTip.Slice(endMethodNameIndex + 1);
            if (!toolTip.IsEmpty && toolTip[0] is ')')
            {
                text.Append(')');
                return toolTip.Slice(1);
            }

            const string indent = "    ";
            text.AppendLineLF().Append(indent);
            while (true)
            {
                // ref/out/in parameters come through the tooltip with the literal text `[ref] `
                // prepended to the type. Unsure why it's the only instance where the square
                // brackets are included, but without special handling it breaks the parser.
                const string RefText = "[ref] ";
                if (toolTip.IndexOf(RefText.AsSpan()) is 0)
                {
                    text.Append(RefText);
                    toolTip = toolTip.Slice(RefText.Length);
                }

                // PowerShell doesn't have a params keyword, though the binder does honor params
                // methods. For lack of a better option that parses well, we'll use the decoration
                // that is added in C# when the keyword is used.
                const string ParamsText = "Params ";
                if (toolTip.IndexOf(ParamsText.AsSpan()) is 0)
                {
                    text.Append("[ParamArray()] ");
                    toolTip = toolTip.Slice(ParamsText.Length);
                }

                // Generics aren't displayed with spaces in the tooltip so this is a safe end of
                // type marker.
                int spaceIndex = toolTip.IndexOf(' ');
                if (spaceIndex is -1)
                {
                    text.Append(toolTip.ToString());
                    return default;
                }

                text.Append('[');
                ProcessType(toolTip.Slice(0, spaceIndex), text, ref usingNamespaces);
                text.Append("] ");
                toolTip = toolTip.Slice(spaceIndex + 1);

                // TODO: Add extra handling if PowerShell/PowerShell#13799 gets merged. This code
                // should mostly handle it fine but a default string value with `,` or `)` would
                // break. That's not the worst if it happens, but extra parsing to handle that might
                // be nice.
                int paramNameEndIndex = toolTip.IndexOfAny(',', ')');
                if (paramNameEndIndex is -1)
                {
                    text.Append(toolTip.ToString());
                    return default;
                }

                text.Append('$').Append(toolTip.Slice(0, paramNameEndIndex).ToString());
                toolTip = toolTip.Slice(paramNameEndIndex);
                if (toolTip[0] is ')')
                {
                    text.Append(')');
                    return toolTip.Slice(1);
                }

                // Skip comma *and* space.
                toolTip = toolTip.Slice(2);

                text.AppendLineLF(",")
                    .Append(indent);
            }
        }

        private static void ProcessType(ReadOnlySpan<char> type, StringBuilder text, ref HashSet<string>? usingNamespaces)
        {
            if (type.IndexOf('[') is int bracketIndex and not -1)
            {
                ProcessType(type.Slice(0, bracketIndex), text, ref usingNamespaces);
                type = type.Slice(bracketIndex);

                // This is an array rather than a generic type.
                if (type.IndexOfAny(',', ']') is 1)
                {
                    text.Append(type.ToString());
                    return;
                }

                text.Append(GenericOpen);
                type = type.Slice(1);
                while (true)
                {
                    if (type.IndexOfAny(',', '[', ']') is int nextDelimIndex and not -1)
                    {
                        ProcessType(type.Slice(0, nextDelimIndex), text, ref usingNamespaces);
                        type = type.Slice(nextDelimIndex);

                        if (type[0] is '[' && type.IndexOfAny(',', ']') is 1)
                        {
                            type = ProcessArray(type, text);
                            continue;
                        }

                        char delimChar = type[0] switch
                        {
                            '[' => GenericOpen,
                            ']' => GenericClose,
                            char c => c,
                        };

                        text.Append(delimChar);
                        type = type.Slice(1);
                        continue;
                    }

                    if (!type.IsEmpty)
                    {
                        text.Append(type.ToString());
                    }

                    return;
                }
            }

            ReadOnlySpan<char> namespaceStart = default;
            int lastDot = 0;
            while (true)
            {
                if (type.IndexOfAny(s_commaSquareBracketOrDot.Span) is int nextDelimIndex and not -1)
                {
                    // Strip namespaces.
                    if (type[nextDelimIndex] is '.')
                    {
                        if (namespaceStart.IsEmpty)
                        {
                            namespaceStart = type;
                        }

                        lastDot += nextDelimIndex + 1;
                        type = type.Slice(nextDelimIndex + 1);
                        continue;
                    }

                    if (!namespaceStart.IsEmpty)
                    {
                        usingNamespaces ??= new(StringComparer.OrdinalIgnoreCase);
                        usingNamespaces.Add(namespaceStart.Slice(0, lastDot - 1).ToString());
                    }

                    text.Append(type.Slice(0, nextDelimIndex).ToString());
                    return;
                }

                if (!namespaceStart.IsEmpty)
                {
                    usingNamespaces ??= new(StringComparer.OrdinalIgnoreCase);
                    usingNamespaces.Add(namespaceStart.Slice(0, lastDot - 1).ToString());
                }

                text.Append(type.ToString());
                return;
            }
        }

        private static ReadOnlySpan<char> ProcessArray(ReadOnlySpan<char> type, StringBuilder text)
        {
            for (int i = 0; i < type.Length; i++)
            {
                char c = type[i];
                if (c is ']')
                {
                    text.Append(']');
                    // Check for types like int[][]
                    if (type.Length - 1 > i && type[i + 1] is '[')
                    {
                        text.Append('[');
                        i++;
                        continue;
                    }

                    return type.Slice(i + 1);
                }

                text.Append(c);
            }

            Debug.Fail("Span passed to ProcessArray should have contained a ']' char.");
            return default;
        }
    }
}
