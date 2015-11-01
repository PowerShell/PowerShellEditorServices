//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("variables")]
    public class VariablesRequest : RequestBase<VariablesRequestArguments>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            VariableDetails[] variables =
                editorSession.DebugService.GetVariables(
                    this.Arguments.VariablesReference);

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    VariablesResponse.Create(variables)));
        }
    }

    public class VariablesRequestArguments
    {
        public int VariablesReference { get; set; }
    }
}

