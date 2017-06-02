﻿//
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
        private string pipeName;
        private NamedPipeServerStream pipeServer;

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string pipeName)
            : base(messageProtocolType)
        {
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
            }
            catch (IOException e)
            {
                Logger.Write(
                    LogLevel.Verbose,
                    "Named pipe server failed to start due to exception:\r\n\r\n" + e.Message);

                throw e;
            }
        }

        public override void Stop()
        {
            if (this.pipeServer != null)
            {
                Logger.Write(LogLevel.Verbose, "Named pipe server shutting down...");

                this.pipeServer.Dispose();

                Logger.Write(LogLevel.Verbose, "Named pipe server has been disposed.");
            }
        }

        private void ListenForConnection()
        {
            Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
#if CoreCLR
                        await this.pipeServer.WaitForConnectionAsync();
#else
                        await Task.Factory.FromAsync(
                            this.pipeServer.BeginWaitForConnection, 
                            this.pipeServer.EndWaitForConnection, null);
#endif
                        this.OnClientConnect(
                            new NamedPipeServerChannel(
                                this.pipeServer));
                    }
                    catch (Exception e)
                    {
                        Logger.WriteException(
                            "An unhandled exception occurred while listening for a named pipe client connection",
                            e);

                        throw e;
                    }
                });
        }
    }
}
