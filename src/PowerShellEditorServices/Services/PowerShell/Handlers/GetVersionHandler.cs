// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class GetVersionHandler : IGetVersionHandler
    {
        public async Task<PowerShellVersion> Handle(GetVersionParams request, CancellationToken cancellationToken)
        {
            return new PowerShellVersion
            {
                Version = VersionUtils.PSVersionString,
                Edition = VersionUtils.PSEdition,
                DisplayVersion = VersionUtils.PSVersion.ToString(2),
                Architecture = VersionUtils.Architecture
            };
        }
    }
}
