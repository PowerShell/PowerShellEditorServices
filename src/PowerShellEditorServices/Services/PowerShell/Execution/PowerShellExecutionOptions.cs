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

        public bool InterruptCommandPrompt { get; set; }

        public bool WriteInputToHost { get; set; }

        public string InputStringToDisplay { get; set; }

        public bool UseNewScope { get; set; }

        internal bool IsReadLine { get; set; }

        internal bool ShouldExecuteInOriginalRunspace { get; set; }
    }
}
