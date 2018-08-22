//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Session
{
    internal class PowerShell5Operations : PowerShell4Operations
    {
        public override void PauseDebugger(Runspace runspace)
        {
#if !PowerShellv3 && !PowerShellv4
            if (runspace.Debugger != null)
            {
                runspace.Debugger.SetDebuggerStepMode(true);
            }
#endif
        }

        public override bool IsDebuggerStopped(PromptNest promptNest, Runspace runspace)
        {
#if !PowerShellv3 && !PowerShellv4
            return runspace.Debugger.InBreakpoint ||
            (promptNest.IsRemote && promptNest.IsInDebugger);
#else
            throw new System.NotSupportedException();
#endif
        }
    }
}

