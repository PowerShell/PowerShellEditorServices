//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    public class MessageWriter
    {
        #region Private Fields

        private TextWriter textWriter;
        private bool includeContentLength;
        private MessageTypeResolver messageTypeResolver;

        private JsonSerializer loggingSerializer = 
            JsonSerializer.Create(
                Constants.JsonSerializerSettings);

        #endregion

        #region Constructors

        public MessageWriter(
            TextWriter textWriter,
            MessageFormat messageFormat,
            MessageTypeResolver messageTypeResolver)
        {
            Validate.IsNotNull("textWriter", textWriter);
            Validate.IsNotNull("messageTypeResolver", messageTypeResolver);

            this.textWriter = textWriter;
            this.messageTypeResolver = messageTypeResolver;
            this.includeContentLength =
                messageFormat == MessageFormat.WithContentLength;
        }

        #endregion

        #region Public Methods

        // TODO: Change back to async?

        public void WriteMessage(MessageBase messageToWrite)
        {
            Validate.IsNotNull("messageToWrite", messageToWrite);

            string messageTypeName = null;
            if (!this.messageTypeResolver.TryGetMessageTypeNameByType(
                    messageToWrite.GetType(),
                    out messageTypeName))
            {
                // TODO: Trace or throw exception?
            }

            // Insert the message's type name before serializing
            messageToWrite.PayloadType = messageTypeName;

            // Log the JSON representation of the message
            Logger.Write(
                LogLevel.Verbose,
                string.Format(
                    "WRITE MESSAGE:\r\n\r\n{0}",
                    JsonConvert.SerializeObject(
                        messageToWrite,
                        Formatting.Indented,
                        Constants.JsonSerializerSettings)));

            // Serialize the message
            string serializedMessage =
                JsonConvert.SerializeObject(
                    messageToWrite,
                    Constants.JsonSerializerSettings);

            // Construct the payload string
            string payloadString = serializedMessage + "\r\n";

            if (this.includeContentLength)
            {
                payloadString = 
                    string.Format(
                        "{0}{1}\r\n\r\n{2}",
                        Constants.ContentLengthString,
                        Encoding.UTF8.GetByteCount(serializedMessage),
                        payloadString);
            }

            // Send the message
            this.textWriter.Write(payloadString);
            this.textWriter.Flush();
        }

        #endregion
    }
}
