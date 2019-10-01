//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal interface IVersionSpecificOperations
    {
        void ConfigureDebugger(Runspace runspace);

        void PauseDebugger(Runspace runspace);

        IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(
            PowerShellContextService powerShellContext,
            Runspace currentRunspace,
            PSCommand psCommand,
            bool sendOutputToHost,
            out DebuggerResumeAction? debuggerResumeAction);

        void StopCommandInDebugger(PowerShellContextService powerShellContext);

        bool IsDebuggerStopped(PromptNest promptNest, Runspace runspace);

        void ExitNestedPrompt(PSHost host);
    }
}

