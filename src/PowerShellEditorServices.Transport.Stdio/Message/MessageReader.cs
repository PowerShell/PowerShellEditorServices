//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    public class MessageReader
    {
        #region Private Fields

        private TextReader textReader;
        private bool expectsContentLength;
        private MessageParser messageParser;
        private char[] buffer = new char[8192];

        #endregion

        #region Constructors

        public MessageReader(
            TextReader textReader,
            MessageFormat messageFormat,
            MessageTypeResolver messageTypeResolver)
        {
            Validate.IsNotNull("textReader", textReader);
            Validate.IsNotNull("messageTypeResolver", messageTypeResolver);

            this.textReader = textReader;
            this.messageParser = new MessageParser(messageTypeResolver);
            this.expectsContentLength =
                messageFormat == MessageFormat.WithContentLength;
        }

        #endregion

        #region Public Methods

        public async Task<MessageBase> ReadMessage()
        {
            string messageLine = await this.textReader.ReadLineAsync();

            // If we're expecting Content-Length lines, check for it
            if (this.expectsContentLength)
            {
                if (messageLine.StartsWith(Constants.ContentLengthString))
                {
                    int contentLength = -1;
                    string contentLengthIntString =
                        messageLine.Substring(
                            Constants.ContentLengthString.Length);

                    // Attempt to parse the Content-Length integer
                    if (!int.TryParse(contentLengthIntString, out contentLength))
                    {
                        throw new MessageParseException(
                            messageLine,
                            "Could not parse integer string provided for Content-Length: {0}",
                            messageLine);
                    }

                    // Make sure Content-Length isn't
                    if (contentLength <= 0)
                    {
                        throw new MessageParseException(
                            messageLine,
                            "Received invalid Content-Length value of {0}",
                            contentLength);
                    }

                    // Skip the next newline
                    await this.textReader.ReadAsync(this.buffer, 0, Environment.NewLine.Length);

                    // NOTE: At this point, we don't actually use the Content-Length
                    // count to read the text because the messages coming from the client
                    // are all on a single line anyway.  We may need to revisit this in
                    // the future.

                    // Read the message content
                    messageLine = await this.textReader.ReadLineAsync();
                }
                else
                {
                    throw new MessageParseException(
                        messageLine,
                        "Unexpected line found while waiting for Content-Length");
                }
            }

            // Return the parsed message
            return this.messageParser.ParseMessage(messageLine);
        }

        #endregion
    }
}
