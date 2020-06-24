namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    public struct PowerShellExecutionOptions
    {
        public static PowerShellExecutionOptions Default = new PowerShellExecutionOptions
        {
            WriteOutputToHost = true,
            WriteErrorsToHost = true,
        };

        public bool WriteOutputToHost { get; set; }

        public bool WriteErrorsToHost { get; set; }

        public bool AddToHistory { get; set; }

        public bool WriteInputToHost { get; set; }

        public bool PropagateCancellationToCaller { get; set; }

        public bool InterruptCommandPrompt { get; set; }
    }
}
