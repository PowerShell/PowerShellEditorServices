//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("occurrences")]
    public class OccurrencesRequest : FileRequest<FileLocationRequestArgs>
    {
        public override Task ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            ScriptFile scriptFile = this.GetScriptFile(editorSession);

            FindOccurrencesResult occurrencesResult =
                editorSession.LanguageService.FindOccurrencesInFile(
                    scriptFile,
                    this.Arguments.Line,
                    this.Arguments.Offset);

            OccurrencesResponse occurrencesResponce = 
                OccurrencesResponse.Create(occurrencesResult, this.Arguments.File);

            messageWriter.WriteMessage(
                this.PrepareResponse(
                    occurrencesResponce));

            return TaskConstants.Completed;
        }
    }
}
