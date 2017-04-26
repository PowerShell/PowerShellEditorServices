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
    public class AbstractMessageType
    {
        private string _method;
        private int _numberOfParams;

        /// <summary>
        /// Gets the method name for the event type.
        /// </summary>
        public string Method { get { return _method; } }

        /// <summary>
        /// Gets the number of parameters.
        /// </summary>
        public int NumberOfParams { get; }

        public AbstractMessageType(string method, int numberOfParams)
        {
            _method = method;
            _numberOfParams = numberOfParams;
        }
    }
}


