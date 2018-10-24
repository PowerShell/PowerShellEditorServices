//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{

    public class ShowHelpRequest
    {
        public static readonly
            RequestType<string, object, object, object> Type =
            RequestType<string, object, object, object>.Create("powerShell/showHelp");
    }
}
