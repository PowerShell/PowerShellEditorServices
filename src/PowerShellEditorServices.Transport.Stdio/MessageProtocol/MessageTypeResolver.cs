//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class MessageTypeResolver
    {
        #region Private Fields

        // Cache a HashSet of raw generic message types for quick comparisons
        private static readonly HashSet<Type> rawGenericMessageTypes =
            new HashSet<Type>(
                new List<Type>
                {
                    typeof(RequestBase<>),
                    typeof(ResponseBase<>),
                    typeof(EventBase<>)
                });

        // Cache a dictionary of raw generic message types to MessageTypes
        private static readonly Dictionary<Type, MessageType> rawGenericTypesToMessageTypes =
            new Dictionary<Type, MessageType>
            {
                { typeof(RequestBase<>),    MessageType.Request },
                { typeof(ResponseBase<>),   MessageType.Response },
                { typeof(EventBase<>),      MessageType.Event },
            };

        private Dictionary<MessageType, Dictionary<string, Type>> typeNameToMessageTypeIndex =
            new Dictionary<MessageType, Dictionary<string, Type>>();

        private Dictionary<Type, string> typeToTypeNameIndex =
            new Dictionary<Type, string>();

        #endregion

        #region Public Methods

        public void ScanForMessageTypes(Assembly sourceAssembly)
        {
            Validate.IsNotNull("sourceAssembly", sourceAssembly);

            // Find all types deriving from MessageBase
            Type messageBaseType = typeof(MessageBase);
            IEnumerable<Type> messageTypes =
                sourceAssembly
                    .GetTypes()
                    .Where(t => messageBaseType.IsAssignableFrom(t) &&
                                t.IsAbstract == false);

            foreach (Type concreteMessageType in messageTypes)
            {
                // Which specific message interface does the type implement?
                MessageType messageType = this.GetMessageTypeOfType(concreteMessageType);

                if (messageType != MessageType.Unknown)
                {
                    this.AddConcreteMessageTypeToIndex(
                        messageType,
                        concreteMessageType);
                }
                else
                {
                    // TODO: Trace warning message
                }
            }
        }

        public bool TryGetMessageTypeByName(
            MessageType messageType,
            string messageTypeName, 
            out Type concreteMessageType)
        {
            Validate.IsNotEqual("messageType", messageType, MessageType.Unknown);
            Validate.IsNotNullOrEmptyString("messageTypeName", messageTypeName);

            concreteMessageType = null;

            Dictionary<string, Type> messageTypeIndex = null;

            if (this.typeNameToMessageTypeIndex.TryGetValue(
                    messageType,
                    out messageTypeIndex))
            {
                return
                    messageTypeIndex.TryGetValue(
                        messageTypeName,
                        out concreteMessageType);
            }

            return false;
        }

        public bool TryGetMessageTypeNameByType(
            Type concreteMessageType, 
            out string messageTypeName)
        {
            Validate.IsNotNull("concreteMessageType", concreteMessageType);

            messageTypeName = null;

            return
                this.typeToTypeNameIndex.TryGetValue(
                    concreteMessageType,
                    out messageTypeName);
        }

        #endregion

        #region Private Helper Methods

        private MessageType GetMessageTypeOfType(Type typeToCheck)
        {
            MessageType messageType = MessageType.Unknown;

            // Walk up the inheritance tree to see if the type to check
            // derives from the given generic type.  Stop if we reach a
            // type that inherits directly from MessageBase (which should
            // only be the generic base types for Request, Response, and
            // Event)
            while (typeToCheck != null &&
                   typeToCheck != typeof(MessageBase))
            {
                // If the current type is a generic type and it
                if (typeToCheck.IsGenericType)
                {
                    // Is the raw generic type one of the message types?
                    Type rawGenericType = typeToCheck.GetGenericTypeDefinition();
                    if (rawGenericMessageTypes.Contains(rawGenericType))
                    {
                        // Find the MessageType corresponding to the generic type
                        if (!rawGenericTypesToMessageTypes.TryGetValue(
                                rawGenericType,
                                out messageType))
                        {
                            // TODO Comment
                            Debug.Assert(false, "BOO");
                        }

                        // Return the message type even if the result will
                        // be Unknown.  Searching further is pointless in
                        // the error condition.
                        return messageType;
                    }
                }

                // Check the type's parent next
                typeToCheck = typeToCheck.BaseType;
            }

            return messageType;
        }

        private void AddConcreteMessageTypeToIndex(
            MessageType messageType,
            Type concreteMessageType)
        {
            // Check for the MessageTypeAttribute
            var messageTypeAttribute =
                concreteMessageType.GetCustomAttribute<MessageTypeNameAttribute>();

            // Assert if the attribute is null
            Debug.Assert(
                messageTypeAttribute != null,
                "Missing MessageTypeAttribute on message type " + concreteMessageType.Name);

            // Try to find the type index for the given message type
            Dictionary<string, Type> messageTypeIndex = null;
            if (!this.typeNameToMessageTypeIndex.TryGetValue(
                    messageType,
                    out messageTypeIndex))
            {
                // Create the index for this MessageType and store it
                messageTypeIndex = new Dictionary<string, Type>();
                this.typeNameToMessageTypeIndex.Add(
                    messageType,
                    messageTypeIndex);
            }

            // Store the concrete message type relative to its MessageType
            messageTypeIndex.Add(
                messageTypeAttribute.MessageTypeName,
                concreteMessageType);

            // Store the relative to its concrete type
            this.typeToTypeNameIndex.Add(
                concreteMessageType,
                messageTypeAttribute.MessageTypeName);
        }

        #endregion
    }
}
