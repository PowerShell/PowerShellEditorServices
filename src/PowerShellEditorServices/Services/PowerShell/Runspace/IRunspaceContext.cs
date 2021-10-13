// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    internal interface IRunspaceContext
    {
        IRunspaceInfo CurrentRunspace { get; }
    }
}
