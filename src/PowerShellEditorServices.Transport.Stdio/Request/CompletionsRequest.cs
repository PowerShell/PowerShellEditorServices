//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("completions")]
    public class CompletionsRequest : FileRequest<CompletionsRequestArgs>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            ScriptFile scriptFile = this.GetScriptFile(editorSession);

            CompletionResults completions =
                await editorSession.LanguageService.GetCompletionsInFile(
                    scriptFile,
                    this.Arguments.Line,
                    this.Arguments.Offset);

            await messageWriter.WriteMessage(
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
