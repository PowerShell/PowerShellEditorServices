//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// Provides the results of a single code completion request.
    /// </summary>
    internal sealed class CompletionResults
    {
        #region Properties

        /// <summary>
        /// Gets the completions that were found during the
        /// completion request.
        /// </summary>
        public CompletionDetails[] Completions { get; private set; }

        /// <summary>
        /// Gets the range in the buffer that should be replaced by this
        /// completion result.
        /// </summary>
        public BufferRange ReplacedRange { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an empty CompletionResults instance.
        /// </summary>
        public CompletionResults()
        {
            this.Completions = Array.Empty<CompletionDetails>();
            this.ReplacedRange = new BufferRange(0, 0, 0, 0);
        }

        internal static CompletionResults Create(
            ScriptFile scriptFile,
            CommandCompletion commandCompletion)
        {
            BufferRange replacedRange = null;

            // Only calculate the replacement range if there are completion results
            if (commandCompletion.CompletionMatches.Count > 0)
            {
                replacedRange =
                    scriptFile.GetRangeBetweenOffsets(
                        commandCompletion.ReplacementIndex,
                        commandCompletion.ReplacementIndex + commandCompletion.ReplacementLength);
            }

            return new CompletionResults
            {
                Completions = GetCompletionsArray(commandCompletion),
                ReplacedRange = replacedRange
            };
        }

        #endregion

        #region Private Methods

        private static CompletionDetails[] GetCompletionsArray(
            CommandCompletion commandCompletion)
        {
            IEnumerable<CompletionDetails> completionList =
                commandCompletion.CompletionMatches.Select(
                    CompletionDetails.Create);

            return completionList.ToArray();
        }

        #endregion
    }

    /// <summary>
    /// Enumerates the completion types that may be returned.
    /// </summary>
    internal enum CompletionType
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
        /// Identifies a completion for a .NET property.
        /// </summary>
        Property,

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
        Keyword,

        /// <summary>
        /// Identifies a completion for a provider path (like a file system path) to a leaf item.
        /// </summary>
        File,

        /// <summary>
        /// Identifies a completion for a provider path (like a file system path) to a container.
        /// </summary>
        Folder
    }

    /// <summary>
    /// Provides the details about a single completion result.
    /// </summary>
    [DebuggerDisplay("CompletionType = {CompletionType.ToString()}, CompletionText = {CompletionText}")]
    internal sealed class CompletionDetails
    {
        #region Properties

        /// <summary>
        /// Gets the text that will be used to complete the statement
        /// at the requested file offset.
        /// </summary>
        public string CompletionText { get; private set; }

        /// <summary>
        /// Gets the text that should be dispayed in a drop-down completion list.
        /// </summary>
        public string ListItemText { get; private set; }

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

        #endregion

        #region Constructors

        internal static CompletionDetails Create(CompletionResult completionResult)
        {
            Validate.IsNotNull("completionResult", completionResult);

            // Some tooltips may have newlines or whitespace for unknown reasons
            string toolTipText = completionResult.ToolTip;
            if (toolTipText != null)
            {
                toolTipText = toolTipText.Trim();
            }

            return new CompletionDetails
            {
                CompletionText = completionResult.CompletionText,
                ListItemText = completionResult.ListItemText,
                ToolTipText = toolTipText,
                SymbolTypeName = ExtractSymbolTypeNameFromToolTip(completionResult.ToolTip),
                CompletionType =
                    ConvertCompletionResultType(
                        completionResult.ResultType)
            };
        }

        internal static CompletionDetails Create(
            string completionText,
            CompletionType completionType,
            string toolTipText = null,
            string symbolTypeName = null,
            string listItemText = null)
        {
            return new CompletionDetails
            {
                CompletionText = completionText,
                CompletionType = completionType,
                ListItemText = listItemText,
                ToolTipText = toolTipText,
                SymbolTypeName = symbolTypeName
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Compares two CompletionResults instances for equality.
        /// </summary>
        /// <param name="obj">The potential CompletionResults instance to compare.</param>
        /// <returns>True if the CompletionResults instances have the same details.</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is CompletionDetails otherDetails))
            {
                return false;
            }

            return
                string.Equals(this.CompletionText, otherDetails.CompletionText) &&
                this.CompletionType == otherDetails.CompletionType &&
                string.Equals(this.ToolTipText, otherDetails.ToolTipText) &&
                string.Equals(this.SymbolTypeName, otherDetails.SymbolTypeName);
        }

        /// <summary>
        /// Returns the hash code for this CompletionResults instance.
        /// </summary>
        /// <returns>The hash code for this CompletionResults instance.</returns>
        public override int GetHashCode()
        {
            return
                string.Format(
                    "{0}{1}{2}{3}{4}",
                    this.CompletionText,
                    this.CompletionType,
                    this.ListItemText,
                    this.ToolTipText,
                    this.SymbolTypeName).GetHashCode();
        }

        #endregion

        #region Private Methods

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

                case CompletionResultType.Property:
                    return CompletionType.Property;

                case CompletionResultType.Variable:
                    return CompletionType.Variable;

                case CompletionResultType.Namespace:
                    return CompletionType.Namespace;

                case CompletionResultType.Type:
                    return CompletionType.Type;

                case CompletionResultType.Keyword:
                case CompletionResultType.DynamicKeyword:
                    return CompletionType.Keyword;

                case CompletionResultType.ProviderContainer:
                    return CompletionType.Folder;

                case CompletionResultType.ProviderItem:
                    return CompletionType.File;

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

        #endregion
    }
}
