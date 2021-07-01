namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    public struct PowerShellExecutionOptions
    {
        public bool WriteOutputToHost { get; set; }

        public bool AddToHistory { get; set; }

        public bool WriteInputToHost { get; set; }

        public bool PropagateCancellationToCaller { get; set; }

        public bool InterruptCommandPrompt { get; set; }

        public bool NoDebuggerExecution { get; set; }
    }
}
