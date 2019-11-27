//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Management.Automation.Language;
using OmnisharpDiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;
using ScriptAnalyzerDiagnosticSeverity = Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticSeverity;

namespace Microsoft.PowerShell.EditorServices.Services.Analysis
{
    internal static class DiagnosticCreationHelper
    {
        // Converts a ParseError from PowerShell into a Diagnostic type.
        internal static Diagnostic FromParseError(ParseError parseError)
        {
            Validate.IsNotNull("parseError", parseError);

            return new Diagnostic
            {
                Message = parseError.Message,
                Severity = OmnisharpDiagnosticSeverity.Error,
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position
                    {
                        Line = parseError.Extent.StartLineNumber - 1,
                        Character = parseError.Extent.StartColumnNumber - 1
                    },
                    End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position
                    {
                        Line = parseError.Extent.EndLineNumber - 1,
                        Character = parseError.Extent.EndColumnNumber - 1
                    }
                },
                Source = "PowerShell"
            };
        }

        // Converts a DiagnosticRecord from PSScriptAnalyzer into a Diagnostic type.
        internal static Diagnostic FromDiagnosticRecord(DiagnosticRecord diagnosticRecord)
        {
            Validate.IsNotNull("diagnosticRecord", diagnosticRecord);

            return new Diagnostic
            {
                Message = $"{diagnosticRecord.Message as string}",
                Code = $"{diagnosticRecord.RuleName as string}",
                Severity = MapDiagnosticSeverity(diagnosticRecord.Severity),
                Source = "PSScriptAnalyzer",
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position
                    {
                        Line = diagnosticRecord.Extent.StartLineNumber - 1,
                        Character = diagnosticRecord.Extent.StartColumnNumber - 1
                    },
                    End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position
                    {
                        Line = diagnosticRecord.Extent.EndLineNumber - 1,
                        Character = diagnosticRecord.Extent.EndColumnNumber - 1
                    }
                }
            };
        }

        private static OmnisharpDiagnosticSeverity MapDiagnosticSeverity(ScriptAnalyzerDiagnosticSeverity diagnosticSeverity)
        {
            switch (diagnosticSeverity)
            {
                case ScriptAnalyzerDiagnosticSeverity.Error:
                    return OmnisharpDiagnosticSeverity.Error;

                case ScriptAnalyzerDiagnosticSeverity.Warning:
                    return OmnisharpDiagnosticSeverity.Warning;

                case ScriptAnalyzerDiagnosticSeverity.Information:
                    return OmnisharpDiagnosticSeverity.Information;

                default:
                    return OmnisharpDiagnosticSeverity.Error;
            }
        }
    }
}
