//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class GetCommandsRequest
    {
        public static readonly
            RequestType<List<PSCommandMessage>, object, object, object> Type =
            RequestType<List<PSCommandMessage>, object, object, object>.Create("powerShell/getCommands");
    }

    public class PSCommandMessage
    {
        public string Name { get; set; }
        public string ModuleName { get; set; }
        public string DefaultParameterSet { get; set; }
        public CommandTypes CommandType { get; set; }
        public Dictionary<string, ParameterMetadata> Parameters { get; set; }
        public System.Collections.ObjectModel.ReadOnlyCollection<CommandParameterSetInfo> ParameterSets { get; set; }
    }
}
