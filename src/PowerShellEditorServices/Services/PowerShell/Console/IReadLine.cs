using System;
using System.Security;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal interface IReadLine
    {
        string ReadLine(CancellationToken cancellationToken);

        SecureString ReadSecureLine(CancellationToken cancellationToken);

        bool TryOverrideReadKey(Func<bool, ConsoleKeyInfo> readKeyOverride);

        bool TryOverrideIdleHandler(Action idleHandler);
    }
}
