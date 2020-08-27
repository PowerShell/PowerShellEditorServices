using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal interface IReadLine
    {
        Task<string> ReadLineAsync(CancellationToken cancellationToken);

        Task<SecureString> ReadSecureLineAsync(CancellationToken cancellationToken);

        string ReadLine();

        SecureString ReadSecureLine();

        bool TryOverrideReadKey(Func<bool, ConsoleKeyInfo> readKeyOverride);

        bool TryOverrideIdleHandler(Action idleHandler);
    }
}
