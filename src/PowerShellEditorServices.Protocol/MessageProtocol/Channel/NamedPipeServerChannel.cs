//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class NamedPipeServerChannel : ChannelBase
    {
        private string pipeName;
        private NamedPipeServerStream pipeServer;

        public NamedPipeServerChannel(string pipeName)
        {
            this.pipeName = pipeName;
        }

        public override async Task WaitForConnection()
        {
#if NanoServer
            await this.pipeServer.WaitForConnectionAsync();
#else
            await Task.Factory.FromAsync(this.pipeServer.BeginWaitForConnection, this.pipeServer.EndWaitForConnection, null);
#endif

            this.IsConnected = true;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.pipeServer =
                new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

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
            if (this.pipeServer != null)
            {
                this.pipeServer.Dispose();
            }
        }
    }
}

