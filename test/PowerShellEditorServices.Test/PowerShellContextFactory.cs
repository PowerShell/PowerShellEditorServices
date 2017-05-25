//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Test.Console;
using System;
using System.IO;

namespace Microsoft.PowerShell.EditorServices.Test
{
    internal static class PowerShellContextFactory
    {

        public static PowerShellContext Create()
        {
            PowerShellContext powerShellContext = new PowerShellContext();
            powerShellContext.Initialize(
                PowerShellContextTests.TestProfilePaths,
                PowerShellContext.CreateRunspace(PowerShellContextTests.TestHostDetails, powerShellContext, false),
                true);

            return powerShellContext;
        }
    }
}
