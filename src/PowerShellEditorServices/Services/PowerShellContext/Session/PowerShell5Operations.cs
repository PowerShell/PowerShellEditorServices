//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal class PowerShell5Operations : IVersionSpecificOperations
    {
        public void ConfigureDebugger(Runspace runspace)
        {
            if (runspace.Debugger != null)
            {
                runspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);
            }
        }

        public virtual void PauseDebugger(Runspace runspace)
        {
            if (runspace.Debugger != null)
            {
                runspace.Debugger.SetDebuggerStepMode(true);
            }
        }

        public virtual bool IsDebuggerStopped(PromptNest promptNest, Runspace runspace)
        {
            return runspace.Debugger.InBreakpoint || (promptNest.IsRemote && promptNest.IsInDebugger);
        }

        public IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(
            PowerShellContextService powerShellContext,
            Runspace currentRunspace,
            PSCommand psCommand,
            bool sendOutputToHost,
            out DebuggerResumeAction? debuggerResumeAction)
        {
            debuggerResumeAction = null;
            PSDataCollection<PSObject> outputCollection = new PSDataCollection<PSObject>();

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

        public void StopCommandInDebugger(PowerShellContextService powerShellContext)
        {
            // If the RunspaceAvailability is None, the runspace is dead and we should not try to run anything in it.
            if (powerShellContext.CurrentRunspace.Runspace.RunspaceAvailability != RunspaceAvailability.None)
            {
                powerShellContext.CurrentRunspace.Runspace.Debugger.StopProcessCommand();
            }
        }

        public void ExitNestedPrompt(PSHost host)
        {
            try
            {
                host.ExitNestedPrompt();
            }
            catch (FlowControlException)
            {
            }
        }
    }
}
