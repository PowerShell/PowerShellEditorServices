//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;

namespace Microsoft.PowerShell.EditorServices.Language
{
    /// <summary>
    /// Provides the results of a single code completion request.
    /// </summary>
    public class CompletionResults
    {
        public int CurrentMatchIndex { get; private set; }

        public CompletionDetails[] Completions { get; private set; }

        internal static CompletionResults Create(
            CommandCompletion commandCompletion)
        {
            return new CompletionResults
            {
                Completions = GetCompletionsArray(commandCompletion),
                CurrentMatchIndex = commandCompletion.CurrentMatchIndex
            };
        }

        private static CompletionDetails[] GetCompletionsArray(
            CommandCompletion commandCompletion)
        {
            IEnumerable<CompletionDetails> completionList =
                commandCompletion.CompletionMatches.Select(
                    CompletionDetails.Create);

            return completionList.ToArray();
        }
    }

    /// <summary>
    /// Enumerates the completion types that may be returned.
    /// </summary>
    public enum CompletionType
    {
        /// <summary>
        /// Completion type is unknown, either through being uninitialized or
        /// having been created from an unsupported CompletionResult that was
        /// returned by the PowerShell engine.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Identifies a completion for a command.
        /// </summary>
        Command,

        /// <summary>
        /// Identifies a completion for a .NET method.
        /// </summary>
        Method,

        /// <summary>
        /// Identifies a completion for a command parameter name.
        /// </summary>
        ParameterName,

        /// <summary>
        /// Identifies a completion for a command parameter value.
        /// </summary>
        ParameterValue,

        /// <summary>
        /// Identifies a completion for a variable name.
        /// </summary>
        Variable,

        /// <summary>
        /// Identifies a completion for a namespace.
        /// </summary>
        Namespace,

        /// <summary>
        /// Identifies a completion for a .NET type name.
        /// </summary>
        Type,

        /// <summary>
        /// Identifies a completion for a PowerShell language keyword.
        /// </summary>
        Keyword
    }

    /// <summary>
    /// Provides the details about a single completion result.
    /// </summary>
    public class CompletionDetails
    {
        /// <summary>
        /// Gets the text that will be used to complete the statement
        /// at the requested file offset.
        /// </summary>
        public string CompletionText { get; private set; }

        /// <summary>
        /// Gets the text that can be used to display a tooltip for
        /// the statement at the requested file offset.
        /// </summary>
        public string ToolTipText { get; private set; }

        /// <summary>
        /// Gets the name of the type which this symbol represents.
        /// If the symbol doesn't have an inherent type, null will
        /// be returned.
        /// </summary>
        public string SymbolTypeName { get; private set; }

        /// <summary>
        /// Gets the CompletionType which identifies the type of this completion.
        /// </summary>
        public CompletionType CompletionType { get; private set; }

        internal static CompletionDetails Create(CompletionResult completionResult)
        {
            //completionResult.ToolTip;
            //completionResult.ListItemText;

            return new CompletionDetails
            {
                CompletionText = completionResult.CompletionText,
                ToolTipText = completionResult.ToolTip,
                SymbolTypeName = ExtractSymbolTypeNameFromToolTip(completionResult.ToolTip),
                CompletionType = 
                    ConvertCompletionResultType(
                        completionResult.ResultType)
            };
        }

        private static CompletionType ConvertCompletionResultType(
            CompletionResultType completionResultType)
        {
            switch (completionResultType)
            {
                case CompletionResultType.Command:
                    return CompletionType.Command;

                case CompletionResultType.Method:
                    return CompletionType.Method;

                case CompletionResultType.ParameterName:
                    return CompletionType.ParameterName;

                case CompletionResultType.ParameterValue:
                    return CompletionType.ParameterValue;

                case CompletionResultType.Variable:
                    return CompletionType.Variable;

                case CompletionResultType.Namespace:
                    return CompletionType.Namespace;

                case CompletionResultType.Type:
                    return CompletionType.Type;

                case CompletionResultType.Keyword:
                    return CompletionType.Keyword;

                default:
                    // TODO: Trace the unsupported CompletionResultType
                    return CompletionType.Unknown;
            }
        }

        private static string ExtractSymbolTypeNameFromToolTip(string toolTipText)
        {
            // Tooltips returned from PowerShell contain the symbol type in
            // brackets.  Attempt to extract such strings for further processing.
            var matches = Regex.Matches(toolTipText, @"^\[(.+)\]");

            if (matches.Count > 0 && matches[0].Groups.Count > 1)
            {         
                // Return the symbol type name
                return matches[0].Groups[1].Value;
            }

            return null;
        }
    }
}
