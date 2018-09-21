//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{

    [Obsolete("This class is deprecated. Use ShowHelpRequest instead.")]
    public class ShowOnlineHelpRequest
    {
        public static readonly
            RequestType<string, object, object, object> Type =
            RequestType<string, object, object, object>.Create("powerShell/showOnlineHelp");
    }
    public class ShowHelpRequest
    {
        public static readonly
            RequestType<string, object, object, object> Type =
            RequestType<string, object, object, object>.Create("powerShell/showHelp");
    }
}
