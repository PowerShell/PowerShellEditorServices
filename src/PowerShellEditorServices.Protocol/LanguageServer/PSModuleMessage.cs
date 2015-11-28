//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class PSModuleMessage
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class PSModuleResponse
    {
        public List<PSModuleMessage> ModuleList { get; set; }
    }
}
