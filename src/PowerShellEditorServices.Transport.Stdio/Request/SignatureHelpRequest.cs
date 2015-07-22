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
    [MessageTypeName("signatureHelp")]
    public class SignatureHelpRequest : FileRequest<SignatureHelpRequestArgs>
    {
        public override void ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            ScriptFile scriptFile = this.GetScriptFile(editorSession);

            ParameterSetSignatures parameterSetSigs =
                editorSession.LanguageService.FindParameterSetsInFile(
                    scriptFile,
                    this.Arguments.Line,
                    this.Arguments.Offset);

            SignatureHelpResponse sigHelpResponce = 
                SignatureHelpResponse.Create(parameterSetSigs);

            messageWriter.WriteMessage(
              this.PrepareResponse(
                  sigHelpResponce));
        }
    }

    public class SignatureHelpRequestArgs : FileLocationRequestArgs
    {
    }
}
