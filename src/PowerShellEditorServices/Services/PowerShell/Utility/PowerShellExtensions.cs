using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class PowerShellExtensions
    {
        public static Collection<TResult> InvokeAndClear<TResult>(this SMA.PowerShell pwsh)
        {
            try
            {
                return pwsh.Invoke<TResult>();
            }
            finally
            {
                pwsh.Commands.Clear();
            }
        }

        public static void InvokeAndClear(this SMA.PowerShell pwsh)
        {
            try
            {
                pwsh.Invoke();
            }
            finally
            {
                pwsh.Commands.Clear();
            }
        }

        public static SMA.PowerShell AddOutputCommand(this SMA.PowerShell pwsh)
        {
            return pwsh.MergePipelineResults()
                .AddCommand("Microsoft.Powershell.Core\\Out-Default", useLocalScope: true);
        }

        public static SMA.PowerShell AddDebugOutputCommand(this SMA.PowerShell pwsh)
        {
            return pwsh.MergePipelineResults()
                .AddCommand("Microsoft.Powershell.Core\\Out-String", useLocalScope: true)
                .AddParameter("Stream");
        }

        public static string GetErrorString(this SMA.PowerShell pwsh)
        {
            var sb = new StringBuilder(capacity: 1024)
                .Append("Execution of the following command(s) completed with errors:")
                .AppendLine()
                .AppendLine()
                .Append(pwsh.Commands.GetInvocationText());

            sb.AddErrorString(pwsh.Streams.Error[0], errorIndex: 1);
            for (int i = 1; i < pwsh.Streams.Error.Count; i++)
            {
                sb.AppendLine().AppendLine();
                sb.AddErrorString(pwsh.Streams.Error[i], errorIndex: i + 1);
            }

            return sb.ToString();
        }

        private static StringBuilder AddErrorString(this StringBuilder sb, ErrorRecord error, int errorIndex)
        {
            sb.Append("Error #").Append(errorIndex).Append(':').AppendLine()
                .Append(error).AppendLine()
                .Append("ScriptStackTrace:").AppendLine()
                .Append(error.ScriptStackTrace ?? "<null>").AppendLine()
                .Append("Exception:").AppendLine()
                .Append("    ").Append(error.Exception.ToString() ?? "<null>");

            Exception innerException = error.Exception?.InnerException;
            while (innerException != null)
            {
                sb.Append("InnerException:").AppendLine()
                    .Append("    ").Append(innerException);
                innerException = innerException.InnerException;
            }

            return sb;
        }

        private static SMA.PowerShell MergePipelineResults(this SMA.PowerShell pwsh)
        {
            Command lastCommand = pwsh.Commands.Commands[pwsh.Commands.Commands.Count - 1];
            lastCommand.MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
            lastCommand.MergeMyResults(PipelineResultTypes.Information, PipelineResultTypes.Output);
            return pwsh;
        }
    }
}
