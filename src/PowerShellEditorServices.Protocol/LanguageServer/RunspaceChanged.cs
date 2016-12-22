//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class RunspaceChangedEvent
    {
        public static readonly
            EventType<RunspaceDetails> Type =
            EventType<RunspaceDetails>.Create("powerShell/runspaceChanged");
    }

    public class RunspaceDetails
    {
        public PowerShellVersion PowerShellVersion { get; set; }

        public RunspaceType RunspaceType { get; set; }

        public string ConnectionString { get; set; }

        public RunspaceDetails()
        {
        }

        public RunspaceDetails(RunspaceChangedEventArgs eventArgs)
        {
            this.PowerShellVersion = new PowerShellVersion(eventArgs.RunspaceVersion);
            this.RunspaceType = eventArgs.RunspaceType;
            this.ConnectionString = eventArgs.ConnectionString;
        }
    }
}
