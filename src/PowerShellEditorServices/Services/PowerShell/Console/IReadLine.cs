// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    // TODO: Do we really need a whole interface for this?
    internal interface IReadLine
    {
        string ReadLine(CancellationToken cancellationToken);
    }
}
