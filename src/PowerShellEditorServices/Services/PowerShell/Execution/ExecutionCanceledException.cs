using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    public class ExecutionCanceledException : Exception
    {
        public ExecutionCanceledException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
