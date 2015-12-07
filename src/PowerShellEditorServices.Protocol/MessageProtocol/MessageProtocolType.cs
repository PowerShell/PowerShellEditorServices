//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    /// <summary>
    /// Defines the possible message protocol types.
    /// </summary>
    public enum MessageProtocolType
    {
        /// <summary>
        /// Identifies the language server message protocol.
        /// </summary>
        LanguageServer,

        /// <summary>
        /// Identifies the debug adapter message protocol.
        /// </summary>
        DebugAdapter
    }
}
