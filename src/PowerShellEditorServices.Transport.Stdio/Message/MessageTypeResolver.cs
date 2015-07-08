//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    internal class MessageTypeResolver
    {
        private Dictionary<Type, Dictionary<string, Type>> typeNameToMessageTypeIndex =
            new Dictionary<Type, Dictionary<string, Type>>();

        public void ScanForMessageTypes(Assembly sourceAssembly)
        {
            Validate.IsNotNull("sourceAssembly", sourceAssembly);

            Type messageInterfaceType = typeof(IMessage);
            Type messageTypeAttributeType = typeof(MessageTypeAttribute);

            // Find all types implementing IMessage
            IEnumerable<Type> messageTypes =
                sourceAssembly
                    .GetTypes()
                    .Where(t => t.IsAssignableFrom(messageInterfaceType));

            foreach (Type messageType in messageTypes)
            {
                // Check for the MessageTypeAttribute
                var messageTypeAttribute = 
                    messageType.GetCustomAttribute<MessageTypeAttribute>();

                // Assert if the attribute is null
                Debug.Assert(
                    messageTypeAttribute != null,
                    "Missing MessageTypeAttribute on message type",
                    "The type {0} is missing a MessageTypeAttribute",
                    messageType.Name);
            }
        }

        public bool TryGetMessageTypeByName(
            Type messageInterfaceType, 
            string messageTypeName, 
            out Type concreteMessageType)
        {
            concreteMessageType = null;

            return false;
        }

        public bool TryGetMessageTypeNameByType(
            Type concreteMessageType, 
            out string messageTypeName)
        {
            messageTypeName = null;

            return false;
        }
    }
}
