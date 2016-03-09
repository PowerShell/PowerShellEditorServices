//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Session
{
    internal interface IVersionSpecificOperations
    {
        void ConfigureDebugger(Runspace runspace);

        void PauseDebugger(Runspace runspace);

        IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(
            PowerShellContext powerShellContext,
            Runspace currentRunspace,
            PSCommand psCommand,
            bool sendOutputToHost);
    }
}

