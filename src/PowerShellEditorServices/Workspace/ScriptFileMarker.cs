﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Contains details for a code correction which can be applied from a ScriptFileMarker.
    /// </summary>
    public class MarkerCorrection
    {
        /// <summary>
        /// Gets or sets the display name of the code correction.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the list of ScriptRegions that define the edits to be made by the correction.
        /// </summary>
        public ScriptRegion[] Edits { get; set; }
    }

    /// <summary>
    /// Defines the message level of a script file marker.
    /// </summary>
    public enum ScriptFileMarkerLevel
    {
        /// <summary>
        /// The marker represents an informational message.
        /// </summary>
        Information = 0,

        /// <summary>
        /// The marker represents a warning message.
        /// </summary>
        Warning,

        /// <summary>
        /// The marker represents an error message.
        /// </summary>
        Error
    };

    /// <summary>
    /// Contains details about a marker that should be displayed
    /// for the a script file.  The marker information could come
    /// from syntax parsing or semantic analysis of the script.
    /// </summary>
    public class ScriptFileMarker
    {
        #region Properties

        /// <summary>
        /// Gets or sets the marker's message string.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the marker's message level.
        /// </summary>
        public ScriptFileMarkerLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the ScriptRegion where the marker should appear.
        /// </summary>
        public ScriptRegion ScriptRegion { get; set; }

        /// <summary>
        /// Gets or sets an optional code correction that can be applied based on this marker.
        /// </summary>
        public MarkerCorrection Correction { get; set; }

        /// <summary>
        /// Gets or sets the name of the marker's source like "PowerShell"
        /// or "PSScriptAnalyzer".
        /// </summary>
        public string Source { get; set; }

        #endregion

        #region Public Methods

        internal static ScriptFileMarker FromParseError(
            ParseError parseError)
        {
            Validate.IsNotNull("parseError", parseError);

            return new ScriptFileMarker
            {
                Message = parseError.Message,
                Level = ScriptFileMarkerLevel.Error,
                ScriptRegion = ScriptRegion.Create(parseError.Extent),
                Source = "PowerShell"
            };
        }
        private static string GetIfExistsString(PSObject psobj, string memberName)
        {
            if (psobj.Members.Match(memberName).Count > 0)
            {
                return psobj.Members[memberName].Value != null ? (string)psobj.Members[memberName].Value : "";
            }
            else
            {
                return "";
            }
        }

        internal static ScriptFileMarker FromDiagnosticRecord(PSObject psObject)
        {
            Validate.IsNotNull("psObject", psObject);
            MarkerCorrection correction = null;

            // make sure psobject is of type DiagnosticRecord
            if (!psObject.TypeNames.Contains(
                    "Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord",
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Input PSObject must of DiagnosticRecord type.");
            }

            // casting psobject to dynamic allows us to access
            // the diagnostic record's properties directly i.e. <instance>.<propertyName>
            // without having to go through PSObject's Members property.
            var diagnosticRecord = psObject as dynamic;
            string ruleName = diagnosticRecord.RuleName as string;

            if (diagnosticRecord.SuggestedCorrections != null)
            {
                var suggestedCorrections = diagnosticRecord.SuggestedCorrections as dynamic;
                List<ScriptRegion> editRegions = new List<ScriptRegion>();
                string correctionMessage = null;
                foreach (var suggestedCorrection in suggestedCorrections)
                {
                    editRegions.Add(new ScriptRegion
                    {
                        File = diagnosticRecord.ScriptPath,
                        Text = suggestedCorrection.Text,
                        StartLineNumber = suggestedCorrection.StartLineNumber,
                        StartColumnNumber = suggestedCorrection.StartColumnNumber,
                        EndLineNumber = suggestedCorrection.EndLineNumber,
                        EndColumnNumber = suggestedCorrection.EndColumnNumber
                    });
                    correctionMessage = suggestedCorrection.Description;
                }

                correction = new MarkerCorrection
                {
                    Name = correctionMessage == null ? diagnosticRecord.Message : correctionMessage,
                    Edits = editRegions.ToArray()
                };
            }

            return new ScriptFileMarker
            {
                Message = $"{diagnosticRecord.Message as string} ({ruleName})",
                Level = GetMarkerLevelFromDiagnosticSeverity((diagnosticRecord.Severity as Enum).ToString()),
                ScriptRegion = ScriptRegion.Create(diagnosticRecord.Extent as IScriptExtent),
                Correction = correction,
                Source = "PSScriptAnalyzer"
            };
        }

        private static ScriptFileMarkerLevel GetMarkerLevelFromDiagnosticSeverity(
            string diagnosticSeverity)
        {
            switch (diagnosticSeverity)
            {
                case "Information":
                    return ScriptFileMarkerLevel.Information;
                case "Warning":
                    return ScriptFileMarkerLevel.Warning;
                case "Error":
                    return ScriptFileMarkerLevel.Error;
                default:
                    throw new ArgumentException(
                        string.Format(
                            "The provided DiagnosticSeverity value '{0}' is unknown.",
                            diagnosticSeverity),
                        "diagnosticSeverity");
            }
        }
        #endregion
    }
}

