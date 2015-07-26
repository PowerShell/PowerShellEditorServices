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
    [MessageTypeName("references")]
    public class ReferencesRequest : FileRequest<FileLocationRequestArgs>
    {
        public override void ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            ScriptFile scriptFile = this.GetScriptFile(editorSession);
            SymbolReference foundSymbol =
                editorSession.LanguageService.FindSymbolAtLocation(
                    scriptFile,
                    this.Arguments.Line,
                    this.Arguments.Offset);

            FindReferencesResult referencesResult =
                editorSession.LanguageService.FindReferencesOfSymbol(
                    foundSymbol,
                    editorSession.ExpandScriptReferences(scriptFile));

            ReferencesResponse referencesResponse = 
                ReferencesResponse.Create(referencesResult, this.Arguments.File);

            messageWriter.WriteMessage(
                this.PrepareResponse(
                    referencesResponse));
        }
    }
}
