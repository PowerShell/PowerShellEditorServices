//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    /// <summary>
    /// Provides the base class for all message types in the 
    /// standard I/O protocol.
    /// </summary>
    public abstract class MessageBase : IMessage
    {
        /// <summary>
        /// Gets or sets the sequence identifier for this message.
        /// </summary>
        public int Seq { get; set; }

        /// <summary>
        /// Gets or sets the string identifying the type of this message.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets the payload type name of this message.  Subclasses
        /// will use this to generalize access to the property that
        /// identifies its protocol payload type.
        /// </summary>
        internal abstract string PayloadType { get; }
    }
}
