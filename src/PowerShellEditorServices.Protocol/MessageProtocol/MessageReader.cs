//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class MessageReader
    {
        #region Private Fields

        public const int DefaultBufferSize = 8192;
        public const double BufferResizeTrigger = 0.25;

        private const int CR = 0x0D;
        private const int LF = 0x0A;
        private static string[] NewLineDelimiters = new string[] { Environment.NewLine }; 

        private Stream inputStream;
        private IMessageSerializer messageSerializer;
        private Encoding messageEncoding;

        private ReadState readState;
        private bool needsMoreData = true;
        private int readOffset;
        private int bufferEndOffset;
        private byte[] messageBuffer = new byte[DefaultBufferSize];

        private int expectedContentLength;
        private Dictionary<string, string> messageHeaders;

        enum ReadState
        {
            Headers,
            Content
        }

        #endregion

        #region Constructors

        public MessageReader(
            Stream inputStream,
            IMessageSerializer messageSerializer,
            Encoding messageEncoding = null)
        {
            Validate.IsNotNull("streamReader", inputStream);
            Validate.IsNotNull("messageSerializer", messageSerializer);

            this.inputStream = inputStream;
            this.messageSerializer = messageSerializer;

            this.messageEncoding = messageEncoding;
            if (messageEncoding == null)
            {
                this.messageEncoding = Encoding.UTF8;
            }

            this.messageBuffer = new byte[DefaultBufferSize];
        }

        #endregion

        #region Public Methods

        public async Task<Message> ReadMessage()
        {
            string messageContent = null;

            // Do we need to read more data or can we process the existing buffer?
            while (!this.needsMoreData || await this.ReadNextChunk())
            {
                // Clear the flag since we should have what we need now
                this.needsMoreData = false;

                // Do we need to look for message headers?
                if (this.readState == ReadState.Headers && 
                    !this.TryReadMessageHeaders())
                {
                    // If we don't have enough data to read headers yet, keep reading
                    this.needsMoreData = true;
                    continue;
                }

                // Do we need to look for message content?
                if (this.readState == ReadState.Content && 
                    !this.TryReadMessageContent(out messageContent))
                {
                    // If we don't have enough data yet to construct the content, keep reading
                    this.needsMoreData = true;
                    continue;
                }

                // We've read a message now, break out of the loop
                break;
            }

            // Get the JObject for the JSON content
            JObject messageObject = JObject.Parse(messageContent);

            // Load the message
            Logger.Write(
                LogLevel.Verbose,
                string.Format(
                    "READ MESSAGE:\r\n\r\n{0}",
                    messageObject.ToString(Formatting.Indented)));

            // Return the parsed message
            return this.messageSerializer.DeserializeMessage(messageObject);
        }

        #endregion

        #region Private Methods

        private async Task<bool> ReadNextChunk()
        {
            // Do we need to resize the buffer?  See if less than 1/4 of the space is left.
            if (((double)(this.messageBuffer.Length - this.bufferEndOffset) / this.messageBuffer.Length) < 0.25)
            {
                // Double the size of the buffer
                Array.Resize(
                    ref this.messageBuffer, 
                    this.messageBuffer.Length * 2);
            }

            // Read the next chunk into the message buffer
            int readLength =
                await this.inputStream.ReadAsync(
                    this.messageBuffer,
                    this.bufferEndOffset,
                    this.messageBuffer.Length - this.bufferEndOffset);

            this.bufferEndOffset += readLength;

            if (readLength == 0)
            {
                // If ReadAsync returns 0 then it means that the stream was
                // closed unexpectedly (usually due to the client application
                // ending suddenly).  For now, just terminate the language
                // server immediately.
                // TODO: Provide a more graceful shutdown path
                throw new EndOfStreamException(
                    "MessageReader's input stream ended unexpectedly, terminating.");
            }

            return true;
        }

        private bool TryReadMessageHeaders()
        {
            int scanOffset = this.readOffset;

            // Scan for the final double-newline that marks the
            // end of the header lines
            while (scanOffset + 3 < this.bufferEndOffset && 
                   (this.messageBuffer[scanOffset] != CR || 
                    this.messageBuffer[scanOffset + 1] != LF || 
                    this.messageBuffer[scanOffset + 2] != CR || 
                    this.messageBuffer[scanOffset + 3] != LF))
            {
                scanOffset++;
            }

            // No header or body separator found (e.g CRLFCRLF)
            if (scanOffset + 3 >= this.bufferEndOffset)
            {
                return false;
            }

            this.messageHeaders = new Dictionary<string, string>();

            var headers = 
                Encoding.ASCII
                    .GetString(this.messageBuffer, this.readOffset, scanOffset)
                    .Split(NewLineDelimiters, StringSplitOptions.RemoveEmptyEntries);

            // Read each header and store it in the dictionary
            foreach (var header in headers)
            {
                int currentLength = header.IndexOf(':');
                if (currentLength == -1)
                {
                    throw new ArgumentException("Message header must separate key and value using :");
                }

                var key = header.Substring(0, currentLength);
                var value = header.Substring(currentLength + 1).Trim();
                this.messageHeaders[key] = value;
            }

            // Make sure a Content-Length header was present, otherwise it
            // is a fatal error
            string contentLengthString = null;
            if (!this.messageHeaders.TryGetValue("Content-Length", out contentLengthString))
            {
                throw new MessageParseException("", "Fatal error: Content-Length header must be provided.");
            }

            // Parse the content length to an integer
            if (!int.TryParse(contentLengthString, out this.expectedContentLength))
            {
                throw new MessageParseException("", "Fatal error: Content-Length value is not an integer.");
            }

            // Skip past the headers plus the newline characters
            this.readOffset += scanOffset + 4;

            // Done reading headers, now read content
            this.readState = ReadState.Content;

            return true;
        }

        private bool TryReadMessageContent(out string messageContent)
        {
            messageContent = null;

            // Do we have enough bytes to reach the expected length?
            if ((this.bufferEndOffset - this.readOffset) < this.expectedContentLength)
            {
                return false;
            }

            // Convert the message contents to a string using the specified encoding
            messageContent = 
                this.messageEncoding.GetString(
                    this.messageBuffer,
                    this.readOffset, 
                    this.expectedContentLength);

            // Move the remaining bytes to the front of the buffer for the next message
            var remainingByteCount = this.bufferEndOffset - (this.expectedContentLength + this.readOffset);
            Buffer.BlockCopy(
                this.messageBuffer, 
                this.expectedContentLength + this.readOffset, 
                this.messageBuffer, 
                0, 
                remainingByteCount);

            // Reset the offsets for the next read
            this.readOffset = 0;
            this.bufferEndOffset = remainingByteCount;

            // Done reading content, now look for headers
            this.readState = ReadState.Headers;

            return true;
        }

        #endregion
    }
}
