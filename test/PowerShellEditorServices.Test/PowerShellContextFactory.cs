//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Test.Console;
using System;
using System.IO;
using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.PowerShell.EditorServices.Console;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Test
{
    internal static class PowerShellContextFactory
    {
        public static PowerShellContext Create(ILogger logger)
        {
            PowerShellContext powerShellContext = new PowerShellContext(logger, isPSReadLineEnabled: false);
            powerShellContext.Initialize(
                PowerShellContextTests.TestProfilePaths,
                PowerShellContext.CreateRunspace(
                    PowerShellContextTests.TestHostDetails,
                    powerShellContext,
                    new TestPSHostUserInterface(powerShellContext, logger),
                    logger),
                true);

            return powerShellContext;
        }
    }

    public class TestPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        public TestPSHostUserInterface(
            PowerShellContext powerShellContext,
            ILogger logger)
            : base(
                powerShellContext,
                new SimplePSHostRawUserInterface(logger),
                Logging.NullLogger)
        {
        }

        public override void WriteOutput(string outputString, bool includeNewLine, OutputType outputType, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
        }

        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            throw new NotImplementedException();
        }

        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            throw new NotImplementedException();
        }

        protected override Task<string> ReadCommandLine(CancellationToken cancellationToken)
        {
            return Task.FromResult("USER COMMAND");
        }

        protected override void UpdateProgress(long sourceId, ProgressDetails progressDetails)
        {
        }
    }
}
