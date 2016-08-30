//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class NamedPipeClientChannel : ChannelBase
    {
        private string pipeName;
        private NamedPipeClientStream pipeClient;

        public NamedPipeClientChannel(string pipeName)
        {
            this.pipeName = pipeName;
        }

        public override async Task WaitForConnection()
        {
#if NanoServer
            await this.pipeClient.ConnectAsync();
#else
            this.IsConnected = false;

            while (!this.IsConnected)
            {
                try
                {
                    // Wait for 500 milliseconds so that we don't tie up the thread
                    this.pipeClient.Connect(500);
                    this.IsConnected = this.pipeClient.IsConnected;
                }
                catch (TimeoutException)
                {
                    // Connect timed out, wait and try again
                    await Task.Delay(1000);
                    continue;
                }
            }
#endif

            // If we've reached this point, we're connected
            this.IsConnected = true;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.pipeClient =
                new NamedPipeClientStream(
                    ".",
                    this.pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

            this.MessageReader =
                new MessageReader(
                    this.pipeClient,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    this.pipeClient,
                    messageSerializer);
        }

        protected override void Shutdown()
        {
            if (this.pipeClient != null)
            {
                this.pipeClient.Dispose();
            }
        }
    }
}

