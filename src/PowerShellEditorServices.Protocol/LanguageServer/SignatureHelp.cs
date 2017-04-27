//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class SignatureHelpRequest
    {
        public static readonly
            RequestType<TextDocumentPositionParams, SignatureHelp, object, object> Type =
            RequestType<TextDocumentPositionParams, SignatureHelp, object, object>.Create("textDocument/signatureHelp");
    }

    public class ParameterInformation
    {
        public string Label { get; set; }

        public string Documentation { get; set; }
    }

    public class SignatureInformation
    {
        public string Label { get; set; }

        public string Documentation { get; set; }

        public ParameterInformation[] Parameters { get; set; }
    }

    public class SignatureHelp
    {
        public SignatureInformation[] Signatures { get; set; }

        public int? ActiveSignature { get; set; }

        public int? ActiveParameter { get; set; }
    }
}

