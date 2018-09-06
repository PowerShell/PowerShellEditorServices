//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Session
{
    internal class PowerShell4Operations : IVersionSpecificOperations
    {
        private static SortedSet<string> s_noHistoryCommandNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "prompt",
            "Set-PSDebuggerAction",
            "Get-PSDebuggerStopArgs",
            "Set-PSDebugMode",
            "TabExpansion2"
        };

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

            // There's no way to tell the debugger not to add the command to history. It does however,
            // check if the first command is in a static list of commands that shouldn't be added
            // to history. We use that here to get around that limitation.
            if (!sendOutputToHost && !s_noHistoryCommandNames.Contains(psCommand.Commands[0].CommandText))
            {
                var newCommand = new PSCommand()
                    .AddCommand("prompt")
                    .AddCommand("Microsoft.PowerShell.Core\\Out-Null")
                    .AddStatement();

                foreach (Command command in psCommand.Commands)
                {
                    newCommand.AddCommand(command);
                }

                psCommand = newCommand;
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
    }
}

