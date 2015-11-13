//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("scopes")]
    public class ScopesRequest : RequestBase<ScopesRequestArgs>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            VariableScope[] variableScopes = 
                editorSession.DebugService.GetVariableScopes(
                    this.Arguments.FrameId);

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new ScopesResponse
                    {
                        Body = new ScopesResponseBody
                        {
                            Scopes = 
                                variableScopes
                                    .Select(Scope.Create)
                                    .ToArray()
                        }
                    }));
        }
    }

    public class ScopesRequestArgs
    {
        public int FrameId { get; set; }
    }
}

