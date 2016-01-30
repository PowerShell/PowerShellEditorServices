//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class FindModuleRequest
    {
        public static readonly
            RequestType<List<PSModuleMessage>, object> Type =
            RequestType<List<PSModuleMessage>, object>.Create("powerShell/findModule");
    }


    public class PSModuleMessage
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
