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
            RequestType<InitializeParams, InitializeResult, InitializeError, object> Type =
            RequestType<InitializeParams, InitializeResult, InitializeError, object>.Create("initialize");
   }

    public enum TraceType {
        Off,
        Messages,
        Verbose
    }

    public class InitializeParams {
        /// <summary>
        /// The process Id of the parent process that started the server
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// The root path of the workspace. It is null if no folder is open.
        ///
        /// This property has been deprecated in favor of RootUri.
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// The root uri of the workspace. It is null if not folder is open. If both
        /// `RootUri` and `RootPath` are non-null, `RootUri` should be used.
        /// </summary>
        public string RootUri { get; set; }

        /// <summary>
        /// The capabilities provided by the client.
        /// </summary>
        public ClientCapabilities Capabilities { get; set; }

        /// <summary>
        /// User provided initialization options.
        ///
        /// This is defined as `any` type on the client side.
        /// </summary>
        public object InitializationOptions { get; set; }

        // TODO We need to verify if the deserializer will map the type defined in the client
        // to an enum.
        /// <summary>
        /// The initial trace setting. If omitted trace is disabled.
        /// </summary>
        public TraceType Trace { get; set; } = TraceType.Off;
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

