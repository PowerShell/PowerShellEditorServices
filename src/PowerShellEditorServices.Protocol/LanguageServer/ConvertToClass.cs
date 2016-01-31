//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class ConvertToCSharpClassRequest
    {
        public static readonly
            RequestType<string, string> Type =
            RequestType<string, string>.Create("powerShell/convertToCSharpClass");
    }

    public class ConvertToPowerShellClassRequest
    {
        public static readonly
            RequestType<string, string> Type =
            RequestType<string, string>.Create("powerShell/convertToPowerShellClass");
    }
}
