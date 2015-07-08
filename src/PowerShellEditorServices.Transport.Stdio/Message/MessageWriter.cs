//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    public class MessageWriter
    {
        #region Private Fields

        private TextWriter textWriter;
        private bool includeContentLength;

        #endregion

        #region Constructors

        public MessageWriter(TextWriter textWriter, MessageFormat messageFormat)
        {
            this.textWriter = textWriter;
            this.includeContentLength =
                messageFormat == MessageFormat.WithContentLength;
        }

        #endregion

        #region Public Methods

        // TODO: Change back to async?

        public void WriteMessage(MessageBase message)
        {
            // Serialize the message
            string serializedMessage = 
                JsonConvert.SerializeObject(
                    message, 
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
