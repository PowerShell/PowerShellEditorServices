//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    /// <summary>
    /// Provides a client implementation for the standard I/O channel.
    /// Launches the server process and then attaches to its console
    /// streams.
    /// </summary>
    public class StdioClientChannel : ChannelBase
    {
        private string serviceProcessPath;
        private string serviceProcessArguments;

        private Stream inputStream;
        private Stream outputStream;
        private Process serviceProcess;

        /// <summary>
        /// Gets the process ID of the server process.
        /// </summary>
        public int ProcessId { get; private set; }

        /// <summary>
        /// Initializes an instance of the StdioClient.
        /// </summary>
        /// <param name="serverProcessPath">The full path to the server process executable.</param>
        /// <param name="serverProcessArguments">Optional arguments to pass to the service process executable.</param>
        public StdioClientChannel(
            string serverProcessPath,
            params string[] serverProcessArguments)
        {
            this.serviceProcessPath = serverProcessPath;

            if (serverProcessArguments != null)
            {
                this.serviceProcessArguments = 
                    string.Join(
                        " ", 
                        serverProcessArguments);
            }
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.serviceProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = this.serviceProcessPath,
                    Arguments = this.serviceProcessArguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };

            // Start the process
            this.serviceProcess.Start();
            this.ProcessId = this.serviceProcess.Id;

            // Open the standard input/output streams
            this.inputStream = this.serviceProcess.StandardOutput.BaseStream;
            this.outputStream = this.serviceProcess.StandardInput.BaseStream;

            // Set up the message reader and writer
            this.MessageReader = 
                new MessageReader(
                    this.inputStream,
                    messageSerializer);

            this.MessageWriter = 
                new MessageWriter(
                    this.outputStream,
                    messageSerializer);

            this.IsConnected = true;
        }

        public override Task WaitForConnection()
        {
            // We're always connected immediately in the stdio channel
            return Task.FromResult(true);
        }

        protected override void Shutdown()
        {
            if (this.inputStream != null)
            {
                this.inputStream.Dispose();
                this.inputStream = null;
            }

            if (this.outputStream != null)
            {
                this.outputStream.Dispose();
                this.outputStream = null;
            }

            if (this.MessageReader != null)
            {
                this.MessageReader = null;
            }

            if (this.MessageWriter != null)
            {
                this.MessageWriter = null;
            }

            this.serviceProcess.Kill();
        }
    }
}
