//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("references")]
    public class ReferencesRequest : FileRequest<FileLocationRequestArgs>
    {
        public override async Task ProcessMessage(
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
                await editorSession.LanguageService.FindReferencesOfSymbol(
                    foundSymbol,
                    editorSession.Workspace.ExpandScriptReferences(scriptFile));

            ReferencesResponse referencesResponse = 
                ReferencesResponse.Create(referencesResult, this.Arguments.File);

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    referencesResponse));
        }
    }
}
