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
    [MessageTypeName("geterr")]
    public class ErrorRequest : RequestBase<ErrorRequestArguments>
    {
        public static ErrorRequest Create(params string[] filePaths)
        {
            return new ErrorRequest
            {
                Arguments = new ErrorRequestArguments
                {
                    Files = filePaths
                }
            };
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

                if (!editorSession.Workspace.TryGetFile(filePath, out scriptFile))
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
                    SyntaxDiagnosticEvent.Create(
                        scriptFile.FilePath,
                        scriptFile.SyntaxMarkers));

                messageWriter.WriteMessage(
                    SemanticDiagnosticEvent.Create(
                        scriptFile.FilePath,
                        semanticMarkers));
            }
        }
    }

    public class ErrorRequestArguments
    {
        public string[] Files { get; set; }

        public int Delay { get; set; }
    }
}
