//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    public class MessageWriter
    {
        #region Private Fields

        private Stream outputStream;
        private MessageTypeResolver messageTypeResolver;

        private JsonSerializer loggingSerializer = 
            JsonSerializer.Create(
                Constants.JsonSerializerSettings);

        #endregion

        #region Constructors

        public MessageWriter(
            Stream outputStream,
            MessageTypeResolver messageTypeResolver)
        {
            Validate.IsNotNull("outputStream", outputStream);
            Validate.IsNotNull("messageTypeResolver", messageTypeResolver);

            this.outputStream = outputStream;
            this.messageTypeResolver = messageTypeResolver;
        }

        #endregion

        #region Public Methods

        public async Task WriteMessage(MessageBase messageToWrite)
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

            byte[] messageBytes = Encoding.UTF8.GetBytes(serializedMessage);
            byte[] headerBytes = 
                Encoding.ASCII.GetBytes(
                    string.Format(
                        Constants.ContentLengthFormatString,
                        messageBytes.Length));

            // Send the message
            await this.outputStream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await this.outputStream.WriteAsync(messageBytes, 0, messageBytes.Length);
            await this.outputStream.FlushAsync();
        }

        #endregion
    }
}
