//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    /// <summary>
    /// Defines an event type with a particular method name.
    /// </summary>
    /// <typeparam name="TParams">The parameter type for this event.</typeparam>
    public class NotificationType<TParams, TRegistrationOptions> : AbstractMessageType
    {
        private NotificationType(string method) : base(method, 1)
        {

        }

        /// <summary>
        /// Creates an EventType instance with the given parameter type and method name.
        /// </summary>
        /// <param name="method">The method name of the event.</param>
        /// <returns>A new EventType instance for the defined type.</returns>
        public static NotificationType<TParams, TRegistrationOptions> Create(string method)
        {
            return new NotificationType<TParams, TRegistrationOptions>(method);
        }
    }
}

