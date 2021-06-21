using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    public enum ExecutionPriority
    {
        Normal,
        Next,
    }

    public record ExecutionOptions
    {
        public static ExecutionOptions Default = new()
        {
            Priority = ExecutionPriority.Normal,
            MustRunInForeground = false,
            InterruptCurrentForeground = false,
        };

        public ExecutionPriority Priority { get; init; }

        public bool MustRunInForeground { get; init; }

        public bool InterruptCurrentForeground { get; init; }
    }

    public record PowerShellExecutionOptions : ExecutionOptions
    {
        public static new PowerShellExecutionOptions Default = new()
        {
            Priority = ExecutionPriority.Normal,
            MustRunInForeground = false,
            InterruptCurrentForeground = false,
            WriteOutputToHost = false,
            WriteInputToHost = false,
            WriteErrorsToHost = false,
            AddToHistory = false,
        };

        public bool WriteOutputToHost { get; init; }

        public bool WriteInputToHost { get; init; }

        public bool WriteErrorsToHost { get; init; }

        public bool AddToHistory { get; init; }
    }
}
