// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    public enum ExecutionPriority
    {
        Normal,
        Next,
    }

    // Some of the fields of this class are not orthogonal,
    // so it's possible to construct self-contradictory execution options.
    // We should see if it's possible to rework this class to make the options less misconfigurable.
    // Generally the executor will do the right thing though; some options just priority over others.
    public record ExecutionOptions
    {
        // This determines which underlying queue the task is added to.
        public ExecutionPriority Priority { get; init; } = ExecutionPriority.Normal;
        // This implies `ExecutionPriority.Next` because foreground tasks are prepended.
        public bool RequiresForeground { get; init; }
    }

    public record PowerShellExecutionOptions : ExecutionOptions
    {
        // TODO: Because of the above, this is actually unnecessary.
        internal static PowerShellExecutionOptions ImmediateInteractive = new()
        {
            Priority = ExecutionPriority.Next,
            RequiresForeground = true,
        };

        public bool WriteOutputToHost { get; init; }
        public bool WriteInputToHost { get; init; }
        public bool ThrowOnError { get; init; } = true;
        public bool AddToHistory { get; init; }
    }
}
