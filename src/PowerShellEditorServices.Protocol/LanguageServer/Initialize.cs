//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class InitializeRequest
    {
        public static readonly
            RequestType<InitializeRequest, InitializeResult> Type =
            RequestType<InitializeRequest, InitializeResult>.Create("initialize");

        /// <summary>
        /// Gets or sets the root path of the editor's open workspace.
        /// If null it is assumed that a file was opened without having
        /// a workspace open.
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// Gets or sets the capabilities provided by the client (editor).
        /// </summary>
        public ClientCapabilities Capabilities { get; set; }
    }

    public class InitializeResult
    {
        /// <summary>
        /// Gets or sets the capabilities provided by the language server.
        /// </summary>
        public ServerCapabilities Capabilities { get; set; }
    }

    public class InitializeError
    {
        /// <summary>
        /// Gets or sets a boolean indicating whether the client should retry
        /// sending the Initialize request after showing the error to the user.
        /// </summary>
        public bool Retry { get; set;}
    }
}

