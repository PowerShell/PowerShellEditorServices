//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("completions")]
    public class CompletionsRequest : FileRequest<CompletionsRequestArgs>
    {
        public override void ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            ScriptFile scriptFile = this.GetScriptFile(editorSession);

            CompletionResults completions =
                editorSession.LanguageService.GetCompletionsInFile(
                    scriptFile,
                    this.Arguments.Line,
                    this.Arguments.Offset);

            messageWriter.WriteMessage(
                this.PrepareResponse(
                    CompletionsResponse.Create(
                        completions)));
        }
    }

    public class CompletionsRequestArgs : FileLocationRequestArgs
    {
        public string Prefix { get; set; }
    }
}
