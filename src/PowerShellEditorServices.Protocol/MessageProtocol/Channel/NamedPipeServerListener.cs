//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class NamedPipeServerListener : ServerListenerBase<NamedPipeServerChannel>
    {
        private ILogger logger;
        private string pipeName;
        private NamedPipeServerStream pipeServer;

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string pipeName,
            ILogger logger)
            : base(messageProtocolType)
        {
            this.logger = logger;
            this.pipeName = pipeName;
        }

        public override void Start()
        {
            try
            {
                this.pipeServer =
                    new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                ListenForConnection();
            }
            catch (IOException e)
            {
                this.logger.Write(
                    LogLevel.Verbose,
                    "Named pipe server failed to start due to exception:\r\n\r\n" + e.Message);

                throw e;
            }
        }

        public override void Stop()
        {
            if (this.pipeServer != null)
            {
                this.logger.Write(LogLevel.Verbose, "Named pipe server shutting down...");

                this.pipeServer.Dispose();

                this.logger.Write(LogLevel.Verbose, "Named pipe server has been disposed.");
            }
        }

        private void ListenForConnection()
        {
            Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        await this.pipeServer.WaitForConnectionAsync();

                        this.OnClientConnect(
                            new NamedPipeServerChannel(
                                this.pipeServer,
                                this.logger));
                    }
                    catch (Exception e)
                    {
                        this.logger.WriteException(
                            "An unhandled exception occurred while listening for a named pipe client connection",
                            e);

                        throw e;
                    }
                });
        }
    }
}
