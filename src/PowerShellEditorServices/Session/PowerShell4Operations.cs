//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Session
{
    internal class PowerShell4Operations : IVersionSpecificOperations
    {
        public void ConfigureDebugger(Runspace runspace)
        {
#if !PowerShellv3
            if (runspace.Debugger != null)
            {
                runspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);
            }
#endif
        }

        public virtual void PauseDebugger(Runspace runspace)
        {
            // The debugger cannot be paused in PowerShell v4.
            throw new NotSupportedException("Debugger cannot be paused in PowerShell v4");
        }

        public IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(
            PowerShellContext powerShellContext,
            Runspace currentRunspace,
            PSCommand psCommand,
            bool sendOutputToHost,
            out DebuggerResumeAction? debuggerResumeAction)
        {
            debuggerResumeAction = null;
            PSDataCollection<PSObject> outputCollection = new PSDataCollection<PSObject>();

#if !PowerShellv3
            if (sendOutputToHost)
            {
                outputCollection.DataAdded +=
                    (obj, e) =>
                    {
                        for (int i = e.Index; i < outputCollection.Count; i++)
                        {
                            powerShellContext.WriteOutput(
                                outputCollection[i].ToString(),
                                true);
                        }
                    };
            }

            DebuggerCommandResults commandResults =
                currentRunspace.Debugger.ProcessCommand(
                    psCommand,
                    outputCollection);

            // Pass along the debugger's resume action if the user's
            // command caused one to be returned
            debuggerResumeAction = commandResults.ResumeAction;
#endif

            IEnumerable<TResult> results = null;
            if (typeof(TResult) != typeof(PSObject))
            {
                results =
                    outputCollection
                        .Select(pso => pso.BaseObject)
                        .Cast<TResult>();
            }
            else
            {
                results = outputCollection.Cast<TResult>();
            }

            return results;
        }

        public void StopCommandInDebugger(PowerShellContext powerShellContext)
        {
#if !PowerShellv3
            powerShellContext.CurrentRunspace.Runspace.Debugger.StopProcessCommand();
#endif
        }

        public virtual bool IsDebuggerStopped(PromptNest promptNest, Runspace runspace)
        {
            return promptNest.IsInDebugger;
        }

        public void ExitNestedPrompt(PSHost host)
        {
#if !PowerShellv3
            try
            {
                host.ExitNestedPrompt();
            }
            catch (FlowControlException)
            {
            }
#else
            throw new NotSupportedException();
#endif
        }
    }
}

