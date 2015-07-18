//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    /// <summary>
    /// Provides the base class for all message types in the 
    /// standard I/O protocol.
    /// </summary>
    public abstract class MessageBase
    {
        /// <summary>
        /// Gets or sets the sequence identifier for this message.
        /// </summary>
        public int Seq { get; set; }

        /// <summary>
        /// Gets or sets the string identifying the type of this message.
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// Gets or sets the payload type name of this message.  Subclasses
        /// will use this to generalize access to the property that
        /// identifies its protocol payload type.
        /// </summary>
        internal abstract string PayloadType { get; set;  }
    }
}
