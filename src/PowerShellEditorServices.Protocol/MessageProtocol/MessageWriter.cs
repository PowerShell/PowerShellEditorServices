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
using System;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class MessageWriter
    {
        #region Private Fields

        private IPsesLogger logger;
        private Stream outputStream;
        private IMessageSerializer messageSerializer;
        private AsyncLock writeLock = new AsyncLock();

        private JsonSerializer contentSerializer =
            JsonSerializer.Create(
                Constants.JsonSerializerSettings);

        #endregion

        #region Constructors

        public MessageWriter(
            Stream outputStream,
            IMessageSerializer messageSerializer,
            IPsesLogger logger)
        {
            Validate.IsNotNull("streamWriter", outputStream);
            Validate.IsNotNull("messageSerializer", messageSerializer);

            this.logger = logger;
            this.outputStream = outputStream;
            this.messageSerializer = messageSerializer;
        }

        #endregion

        #region Public Methods

        // TODO: This method should be made protected or private

        public async Task WriteMessage(Message messageToWrite)
        {
            Validate.IsNotNull("messageToWrite", messageToWrite);

            // Serialize the message
            JObject messageObject =
                this.messageSerializer.SerializeMessage(
                    messageToWrite);

            this.logger.Write(
                LogLevel.Verbose,
                $"Writing {messageToWrite.MessageType} '{messageToWrite.Method}'" +
                (!string.IsNullOrEmpty(messageToWrite.Id) ? $" with id {messageToWrite.Id}" : string.Empty));

            // Log the JSON representation of the message
            this.logger.Write(
                LogLevel.Diagnostic,
                string.Format(
                    "WRITE MESSAGE:\r\n\r\n{0}",
                    JsonConvert.SerializeObject(
                        messageObject,
                        Formatting.Indented,
                        Constants.JsonSerializerSettings)));

            string serializedMessage =
                JsonConvert.SerializeObject(
                    messageObject,
                    Constants.JsonSerializerSettings);

            byte[] messageBytes = Encoding.UTF8.GetBytes(serializedMessage);
            byte[] headerBytes =
                Encoding.ASCII.GetBytes(
                    string.Format(
                        Constants.ContentLengthFormatString,
                        messageBytes.Length));

            // Make sure only one call is writing at a time.  You might be thinking
            // "Why not use a normal lock?"  We use an AsyncLock here so that the
            // message loop doesn't get blocked while waiting for I/O to complete.
            using (await this.writeLock.LockAsync())
            {
                // Send the message
                await this.outputStream.WriteAsync(headerBytes, 0, headerBytes.Length);
                await this.outputStream.WriteAsync(messageBytes, 0, messageBytes.Length);
                await this.outputStream.FlushAsync();
            }
        }

        public async Task WriteRequest<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            TParams requestParams,
            int requestId)
        {
            // Allow null content
            JToken contentObject =
                requestParams != null ?
                    JToken.FromObject(requestParams, contentSerializer) :
                    null;

            await this.WriteMessage(
                Message.Request(
                    requestId.ToString(),
                    requestType.Method,
                    contentObject));
        }

        public async Task WriteResponse<TResult>(TResult resultContent, string method, string requestId)
        {
            // Allow null content
            JToken contentObject =
                resultContent != null ?
                    JToken.FromObject(resultContent, contentSerializer) :
                    null;

            await this.WriteMessage(
                Message.Response(
                    requestId,
                    method,
                    contentObject));
        }

        public async Task WriteEvent<TParams, TRegistrationOptions>(NotificationType<TParams, TRegistrationOptions> eventType, TParams eventParams)
        {
            // Allow null content
            JToken contentObject =
                eventParams != null ?
                    JToken.FromObject(eventParams, contentSerializer) :
                    null;

            await this.WriteMessage(
                Message.Event(
                    eventType.Method,
                    contentObject));
        }

        #endregion
    }
}
