//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    /// <summary>
    /// Provides the base inerface for all message types in the 
    /// standard I/O protocol.
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets the sequence identifier for this message.
        /// </summary>
        int Seq { get; }

        /// <summary>
        /// Gets the string identifying the type of this message.
        /// </summary>
        string Type { get; }
    }

}
