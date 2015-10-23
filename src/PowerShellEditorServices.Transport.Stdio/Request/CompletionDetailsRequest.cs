//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("completionEntryDetails")]
    public class CompletionDetailsRequest : FileRequest<CompletionDetailsRequestArgs>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            ScriptFile scriptFile = this.GetScriptFile(editorSession);

            CompletionDetails completionDetails =
                editorSession.LanguageService.GetCompletionDetailsInFile(
                    scriptFile,
                    this.Arguments.Line,
                    this.Arguments.Offset,
                    this.Arguments.EntryNames[0]);

            var details = new List<CompletionEntryDetails>();
            if (completionDetails != null)
            {
                details.Add(
                    new CompletionEntryDetails(completionDetails, this.Arguments.EntryNames[0]
                        ));
                await messageWriter.WriteMessage(
                    this.PrepareResponse(
                        new CompletionDetailsResponse
                        {
                            Body = details.ToArray()
                        }));
            }
            else
            {
                await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new CompletionDetailsResponse{
                        Body = details.ToArray()
                    }));
            }
        }
    }

    public class CompletionDetailsRequestArgs : FileLocationRequestArgs
    {
        public string[] EntryNames { get; set; }
    }

}
