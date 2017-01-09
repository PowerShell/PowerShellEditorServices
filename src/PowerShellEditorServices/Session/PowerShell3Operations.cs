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
    internal class PowerShell3Operations : IVersionSpecificOperations
    {
        public void ConfigureDebugger(Runspace runspace)
        {
            // The debugger has no SetDebugMode in PowerShell v3.
        }

        public void PauseDebugger(Runspace runspace)
        {
            // The debugger cannot be paused in PowerShell v3.
            throw new NotSupportedException("Debugger cannot be paused in PowerShell v3");
        }

        public IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(
            PowerShellContext powerShellContext,
            Runspace currentRunspace,
            PSCommand psCommand,
            bool sendOutputToHost)
        {
            IEnumerable<TResult> executionResult = null;

            using (var nestedPipeline = currentRunspace.CreateNestedPipeline())
            {
                foreach (var command in psCommand.Commands)
                {
                    nestedPipeline.Commands.Add(command);
                }

                var results = nestedPipeline.Invoke();

                if (typeof(TResult) != typeof(PSObject))
                {
                    executionResult =
                        results
                            .Select(pso => pso.BaseObject)
                            .Cast<TResult>();
                }
                else
                {
                    executionResult = results.Cast<TResult>();
                }
            }

            // Write the output to the host if necessary
            if (sendOutputToHost)
            {
                foreach (var line in executionResult)
                {
                    powerShellContext.WriteOutput(line.ToString(), true);
                }
            }

            return executionResult;
        }
    }
}

