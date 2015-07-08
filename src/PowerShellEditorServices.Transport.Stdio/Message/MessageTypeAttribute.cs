//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    /// <summary>
    /// Marks a type deriving from MessageBase with a name that is
    /// used to identify the message's type.  This is exposed as the
    /// "command" field for Requests and Responses and the "event"
    /// field for Events.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal class MessageTypeAttribute : Attribute
    {
        /// <summary>
        /// Gets the message type's name.
        /// </summary>
        public string MessageTypeName { get; private set; }

        /// <summary>
        /// Creates an instance of the MessageTypeAttribute class with
        /// the given messageTypeName.
        /// </summary>
        /// <param name="messageTypeName">The type name for this message class.</param>
        public MessageTypeAttribute(string messageTypeName)
        {
            Validate.IsNotNullOrEmptyString("messageTypeName", messageTypeName);

            this.MessageTypeName = messageTypeName;
        }
    }
}
