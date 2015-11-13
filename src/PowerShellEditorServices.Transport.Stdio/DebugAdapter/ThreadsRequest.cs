//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("threads")]
    public class ThreadsRequest : RequestBase<object>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new ThreadsResponse
                    {
                        Body = new ThreadsResponseBody
                        {
                            Threads = new Thread[]
                            {
                                // TODO: What do I do with these?
                                new Thread
                                {
                                    Id = 1,
                                    Name = "Main Thread"
                                }
                            }
                        }
                    }));
        }
    }
}

