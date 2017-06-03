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
        private ILogger logger;
        private NamedPipeServerStream pipeServer;

        public NamedPipeServerChannel(
            NamedPipeServerStream pipeServer,
            ILogger logger)
        {
            this.pipeServer = pipeServer;
            this.logger = logger;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.MessageReader =
                new MessageReader(
                    this.pipeServer,
                    messageSerializer,
                    this.logger);

            this.MessageWriter =
                new MessageWriter(
                    this.pipeServer,
                    messageSerializer,
                    this.logger);
        }

        protected override void Shutdown()
        {
            // The server listener will take care of the pipe server
            this.pipeServer = null;
        }
    }
}

