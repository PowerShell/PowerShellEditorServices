//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Text;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    /// <summary>
    /// Provides a server implementation for the standard I/O channel.
    /// When started in a process, attaches to the console I/O streams
    /// to communicate with the client that launched the process.
    /// </summary>
    public class StdioServerChannel : ChannelBase
    {
        private ILogger logger;
        private Stream inputStream;
        private Stream outputStream;

        public StdioServerChannel(ILogger logger)
        {
            this.logger = logger;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
#if !CoreCLR
            // Ensure that the console is using UTF-8 encoding
            System.Console.InputEncoding = Encoding.UTF8;
            System.Console.OutputEncoding = Encoding.UTF8;
#endif

            // Open the standard input/output streams
            this.inputStream = System.Console.OpenStandardInput();
            this.outputStream = System.Console.OpenStandardOutput();

            // Set up the reader and writer
            this.MessageReader =
                new MessageReader(
                    this.inputStream,
                    messageSerializer,
                    this.logger);

            this.MessageWriter =
                new MessageWriter(
                    this.outputStream,
                    messageSerializer,
                    this.logger);
        }

        protected override void Shutdown()
        {
            // No default implementation needed, streams will be
            // disposed on process shutdown.
        }
    }
}
