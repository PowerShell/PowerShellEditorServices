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
    [MessageTypeName("definition")]
    public class DeclarationRequest : FileRequest<FileLocationRequestArgs>
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

            GetDefinitionResult definition =
                editorSession.LanguageService.GetDefinitionOfSymbol(
                    foundSymbol,
                    editorSession.Workspace.ExpandScriptReferences(scriptFile));

            if (definition != null)
            {
                DefinitionResponse defResponse;
                if (definition.FoundDefinition != null)
                {
                    defResponse = DefinitionResponse.Create(definition.FoundDefinition);
                }
                else
                {
                    defResponse = DefinitionResponse.Create();
                }

                messageWriter.WriteMessage(
                    this.PrepareResponse(defResponse));
            }
        }
    }
}
