using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Provides platform specific console utilities.
    /// </summary>
    public interface IConsoleOperations
    {
        /// <summary>
        /// Obtains the next character or function key pressed by the user asynchronously.
        /// Does not block when other console API's are called.
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken to observe.</param>
        /// <returns>
        /// A task that will complete with a result of the key pressed by the user.
        /// </returns>
        Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken);
    }
}
