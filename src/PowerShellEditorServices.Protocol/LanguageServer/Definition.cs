//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class DefinitionRequest
    {
        public static readonly
            RequestType<TextDocumentPosition, Location[], object, TextDocumentRegistrationOptions> Type =
            RequestType<TextDocumentPosition, Location[], object, TextDocumentRegistrationOptions>.Create("textDocument/definition");
    }
}

