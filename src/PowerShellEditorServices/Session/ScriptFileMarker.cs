//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Session
{
    public enum ScriptFileMarkerLevel
    {
        Information = 0,

        Warning,

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

        public string Message { get; set; }

        public ScriptFileMarkerLevel Level { get; set; }

        public IScriptExtent Extent { get; set; }

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
                Extent = parseError.Extent
            };
        }

        internal static ScriptFileMarker FromDiagnosticRecord(
            DiagnosticRecord diagnosticRecord)
        {
            Validate.IsNotNull("diagnosticRecord", diagnosticRecord);

            return new ScriptFileMarker
            {
                Message = diagnosticRecord.Message,
                Level = GetMarkerLevelFromDiagnosticSeverity(diagnosticRecord.Severity),
                Extent = diagnosticRecord.Extent,
            };
        }

        private static ScriptFileMarkerLevel GetMarkerLevelFromDiagnosticSeverity(
            DiagnosticSeverity diagnosticSeverity)
        {
            switch (diagnosticSeverity)
            {
                case DiagnosticSeverity.Information:
                    return ScriptFileMarkerLevel.Information;
                case DiagnosticSeverity.Warning:
                    return ScriptFileMarkerLevel.Warning;
                case DiagnosticSeverity.Error:
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
