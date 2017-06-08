//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// Code Lens options.
    /// </summary>
    public class CodeLensOptions
    {
        /// <summary>
        /// Code lens has a resolve provider as well.
        /// </summary>
        public bool ResolveProvider { get; set; }
    }

    public class CodeLens
    {
        public Range Range { get; set; }

        public ServerCommand Command { get; set; }

        public JToken Data { get; set; }
    }

    /// <summary>
    /// A code lens represents a command that should be shown along with
    /// source text, like the number of references, a way to run tests, etc.
    ///
    /// A code lens is _unresolved_ when no command is associated to it.  For performance
    /// reasons the creation of a code lens and resolving should be done in two stages.
    /// </summary>
    public class CodeLensRequest
    {
        public static readonly
            RequestType<CodeLensRequest, CodeLens[], object, object> Type =
            RequestType<CodeLensRequest, CodeLens[], object, object>.Create("textDocument/codeLens");

        /// <summary>
        /// The document to request code lens for.
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    public class CodeLensResolveRequest
    {
        public static readonly
            RequestType<CodeLens, CodeLens, object, object> Type =
            RequestType<CodeLens, CodeLens, object, object>.Create("codeLens/resolve");
    }
}
