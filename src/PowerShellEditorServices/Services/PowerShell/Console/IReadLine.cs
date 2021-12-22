// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal interface IReadLine
    {
        string ReadLine(CancellationToken cancellationToken);

        SecureString ReadSecureLine(CancellationToken cancellationToken);
    }
}
