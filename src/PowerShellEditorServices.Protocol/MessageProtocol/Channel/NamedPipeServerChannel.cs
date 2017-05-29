//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System.IO.Pipes;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class NamedPipeServerChannel : ChannelBase
    {
        private NamedPipeServerStream pipeServer;

        public NamedPipeServerChannel(NamedPipeServerStream pipeServer)
        {
            this.pipeServer = pipeServer;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.MessageReader =
                new MessageReader(
                    this.pipeServer,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    this.pipeServer,
                    messageSerializer);
        }

        protected override void Shutdown()
        {
            // The server listener will take care of the pipe server
            this.pipeServer = null;
        }
    }
}

