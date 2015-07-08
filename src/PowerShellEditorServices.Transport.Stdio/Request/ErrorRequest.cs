//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    public class ErrorRequest : RequestBase<ErrorRequestArguments>
    {
        public ErrorRequest()
        {
            this.Command = "geterr";
            this.Arguments = new ErrorRequestArguments();
        }

        public ErrorRequest(params string[] filePaths) : this()
        {
            this.Arguments.Files = filePaths;
        }

        public override void ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            List<ScriptFile> fileList = new List<ScriptFile>();

            // Get the requested files
            foreach (string filePath in this.Arguments.Files)
            {
                ScriptFile scriptFile = null;

                if (!editorSession.TryGetFile(filePath, out scriptFile))
                {
                    // Skip this file and log the file load error
                    // TODO: Trace out the error message
                    continue;
                }

                var semanticMarkers =
                    editorSession.AnalysisService.GetSemanticMarkers(
                        scriptFile);

                // Always send syntax and semantic errors.  We want to 
                // make sure no out-of-date markers are being displayed.
                messageWriter.WriteMessage(
                    new DiagnosticEvent("syntaxDiag")
                    {
                        Body = new DiagnosticEventBody
                        {
                            File = filePath,
                            Diagnostics = this.GetSyntaxDiagnostics(scriptFile),
                        }
                    });
                messageWriter.WriteMessage(
                    new DiagnosticEvent("semanticDiag")
                    {
                        Body = new DiagnosticEventBody
                        {
                            File = filePath,
                            Diagnostics = this.GetSemanticDiagnostics(semanticMarkers)
                        }
                    });
            }
        }

        private Diagnostic[] GetSyntaxDiagnostics(ScriptFile file)
        {
            List<Diagnostic> errorList = new List<Diagnostic>();

            foreach (ScriptFileMarker syntaxMarker in file.SyntaxMarkers)
            {
                errorList.Add(
                    new Diagnostic
                    {
                        Text = syntaxMarker.Message,
                        Start = new Location
                        {
                            Line = syntaxMarker.Extent.StartLineNumber,
                            Offset = syntaxMarker.Extent.StartColumnNumber
                        },
                        End = new Location
                        {
                            Line = syntaxMarker.Extent.EndLineNumber,
                            Offset = syntaxMarker.Extent.EndColumnNumber
                        }
                    });
            }

            return errorList.ToArray();
        }

        private Diagnostic[] GetSemanticDiagnostics(IEnumerable<ScriptFileMarker> semanticMarkers)
        {
            List<Diagnostic> diagnosticList = new List<Diagnostic>();

            foreach (ScriptFileMarker semanticMarker in semanticMarkers)
            {
                diagnosticList.Add(
                    new Diagnostic
                    {
                        Text = semanticMarker.Message,
                        Start = new Location
                        {
                            Line = semanticMarker.Extent.StartLineNumber,
                            Offset = semanticMarker.Extent.StartColumnNumber
                        },
                        End = new Location
                        {
                            Line = semanticMarker.Extent.EndLineNumber,
                            Offset = semanticMarker.Extent.EndColumnNumber
                        }
                    });
            }

            return diagnosticList.ToArray();
        }
    }

    public class ErrorRequestArguments
    {
        public string[] Files { get; set; }

        public int Delay { get; set; }
    }
}
