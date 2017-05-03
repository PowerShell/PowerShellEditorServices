//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class ReferencesRequest
    {
        public static readonly
            RequestType<ReferencesParams, Location[], object, TextDocumentRegistrationOptions> Type =
            RequestType<ReferencesParams, Location[], object, TextDocumentRegistrationOptions>.Create("textDocument/references");
    }

    public class ReferencesParams : TextDocumentPositionParams
    {
        public ReferencesContext Context { get; set; }
    }

    public class ReferencesContext
    {
        public bool IncludeDeclaration { get; set; }
    }
}

