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
        private NamedPipeServerStream inOutPipeServer;
        private NamedPipeServerStream outPipeServer;

        public NamedPipeServerChannel(
            NamedPipeServerStream inOutPipeServer,
            ILogger logger)
        {
            this.inOutPipeServer = inOutPipeServer;
            this.outPipeServer = null;
            this.logger = logger;
        }
        public NamedPipeServerChannel(
            NamedPipeServerStream inOutPipeServer,
            NamedPipeServerStream outPipeServer,
            ILogger logger)
        {
            this.inOutPipeServer = inOutPipeServer;
            this.outPipeServer = outPipeServer;
            this.logger = logger;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.MessageReader =
                new MessageReader(
                    this.inOutPipeServer,
                    messageSerializer,
                    this.logger);

            this.MessageWriter =
                new MessageWriter(
                    this.outPipeServer ?? this.inOutPipeServer,
                    messageSerializer,
                    this.logger);
        }

        protected override void Shutdown()
        {
            // The server listener will take care of the pipe server
            this.inOutPipeServer = null;
            this.outPipeServer = null;
        }
    }
}

