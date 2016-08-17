//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
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
                ScriptRegion = ScriptRegion.Create(parseError.Extent)
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
            return new ScriptFileMarker
            {
                Message = GetIfExistsString(psObject, "Message"),
                //Level = GetMarkerLevelFromDiagnosticSeverity(GetIfExistsString(psObject, "Severity")),
                Level = GetMarkerLevelFromDiagnosticSeverity("Warning"),
                ScriptRegion = ScriptRegion.Create((IScriptExtent)psObject.Members["Extent"].Value)
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

