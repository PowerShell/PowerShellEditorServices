//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    [MessageTypeName("syntaxDiag")]
    public class SyntaxDiagnosticEvent : EventBase<DiagnosticEventBody>
    {
        public static SyntaxDiagnosticEvent Create(
            string filePath,
            ScriptFileMarker[] syntaxMarkers)
        {
            return new SyntaxDiagnosticEvent
            {
                Body =
                    DiagnosticEventBody.Create(
                        filePath,
                        syntaxMarkers)
            };
        }
    }

    [MessageTypeName("semanticDiag")]
    public class SemanticDiagnosticEvent : EventBase<DiagnosticEventBody>
    {
        public static SemanticDiagnosticEvent Create(
            string filePath,
            ScriptFileMarker[] semanticMarkers)
        {
            return new SemanticDiagnosticEvent
            {
                Body =
                    DiagnosticEventBody.Create(
                        filePath,
                        semanticMarkers)
            };
        }
    }

    public class DiagnosticEventBody
    {
        public string File { get; set; }

        public Diagnostic[] Diagnostics { get; set; }

        public static DiagnosticEventBody Create(
            string filePath,
            ScriptFileMarker[] diagnosticMarkers)
        {
            List<Diagnostic> diagnosticList = new List<Diagnostic>();

            foreach (ScriptFileMarker diagnosticMarker in diagnosticMarkers)
            {
                diagnosticList.Add(
                    new Diagnostic
                    {
                        Text = diagnosticMarker.Message,
                        Severity = (int)diagnosticMarker.Level,
                        Start = new Location
                        {
                            Line = diagnosticMarker.ScriptRegion.StartLineNumber,
                            Offset = diagnosticMarker.ScriptRegion.StartColumnNumber
                        },
                        End = new Location
                        {
                            Line = diagnosticMarker.ScriptRegion.EndLineNumber,
                            Offset = diagnosticMarker.ScriptRegion.EndColumnNumber
                        }
                    });
            }

            return
                new DiagnosticEventBody
                {
                    File = filePath,
                    Diagnostics = diagnosticList.ToArray()
                };
        }
    }

    public class Location
    {
        public int Line { get; set; }

        public int Offset { get; set; }
    }

    public class Diagnostic
    {
        public Location Start { get; set; }

        public Location End { get; set; }

        public string Text { get; set; }

        public int Severity { get; set; }
    }
}
