//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{

    /// <summary>
    /// Describes the request to get the details for PowerShell Commands from the current session.
    /// </summary>
        public class GetCommandRequest
    {
        public static readonly
            RequestType<string, object, object, object> Type =
            RequestType<string, object, object, object>.Create("powerShell/getCommand");
    }

    /// <summary>
    /// Describes the message to get the details for a single PowerShell Command
    /// from the current session
    /// </summary>
    public class PSCommandMessage
    {
        public string Name { get; set; }
        public string ModuleName { get; set; }
        public string DefaultParameterSet { get; set; }
        public Dictionary<string, ParameterMetadata> Parameters { get; set; }
        public System.Collections.ObjectModel.ReadOnlyCollection<CommandParameterSetInfo> ParameterSets { get; set; }
    }
}
