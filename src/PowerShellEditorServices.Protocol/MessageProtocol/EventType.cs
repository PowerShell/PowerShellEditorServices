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
    public class EventType<TParams>
    {
        /// <summary>
        /// Gets the method name for the event type.
        /// </summary>
        public string MethodName { get; private set; }

        /// <summary>
        /// Creates an EventType instance with the given parameter type and method name.
        /// </summary>
        /// <param name="methodName">The method name of the event.</param>
        /// <returns>A new EventType instance for the defined type.</returns>
        public static EventType<TParams> Create(string methodName)
        {
            return new EventType<TParams>()
            {
                MethodName = methodName
            };
        }
    }
}

