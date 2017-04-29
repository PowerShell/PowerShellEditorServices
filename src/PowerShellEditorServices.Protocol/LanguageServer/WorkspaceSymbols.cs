//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public enum SymbolKind
    {
        File = 1,
        Module = 2,
        Namespace = 3,
        Package = 4,
        Class = 5,
        Method = 6,
        Property = 7,
        Field = 8,
        Constructor = 9,
        Enum = 10,
        Interface = 11,
        Function = 12,
        Variable = 13,
        Constant = 14,
        String = 15,
        Number = 16,
        Boolean = 17,
        Array = 18,
    }

    public class SymbolInformation
    {
        public string Name { get; set; }

        public SymbolKind Kind { get; set; }

        public Location Location { get; set; }

        public string ContainerName { get; set;}
    }

    public class DocumentSymbolRequest
    {
        public static readonly
            RequestType<DocumentSymbolParams, SymbolInformation[], object, TextDocumentRegistrationOptions> Type =
            RequestType<DocumentSymbolParams, SymbolInformation[], object, TextDocumentRegistrationOptions>.Create("textDocument/documentSymbol");
    }

    /// <summary>
    /// Parameters for a DocumentSymbolRequest
    /// </summary>
    public class DocumentSymbolParams
    {
        /// <summary>
        /// The text document.
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    public class WorkspaceSymbolRequest
    {
        public static readonly
            RequestType<WorkspaceSymbolParams, SymbolInformation[], object, object> Type =
            RequestType<WorkspaceSymbolParams, SymbolInformation[], object, object>.Create("workspace/symbol");
    }

    public class WorkspaceSymbolParams
    {
        public string Query { get; set;}
    }
}

