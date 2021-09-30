﻿using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class PSCommandExtensions
    {
        public static PSCommand AddOutputCommand(this PSCommand psCommand)
        {
            return psCommand.MergePipelineResults()
                .AddCommand("Out-Default", useLocalScope: true);
        }

        public static PSCommand AddDebugOutputCommand(this PSCommand psCommand)
        {
            return psCommand.MergePipelineResults()
                .AddCommand("Out-String", useLocalScope: true)
                .AddParameter("Stream");
        }

        public static PSCommand MergePipelineResults(this PSCommand psCommand)
        {
            // We need to do merge errors and output before rendering with an Out- cmdlet
            Command lastCommand = psCommand.Commands[psCommand.Commands.Count - 1];
            lastCommand.MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
            lastCommand.MergeMyResults(PipelineResultTypes.Information, PipelineResultTypes.Output);
            return psCommand;
        }

        /// <summary>
        /// Get a representation of the PSCommand, for logging purposes.
        /// </summary>
        public static string GetInvocationText(this PSCommand command)
        {
            Command currentCommand = command.Commands[0];
            var sb = new StringBuilder().AddCommandText(command.Commands[0]);

            for (int i = 1; i < command.Commands.Count; i++)
            {
                sb.Append(currentCommand.IsEndOfStatement ? "; " : " | ");
                currentCommand = command.Commands[i];
                sb.AddCommandText(currentCommand);
            }

            return sb.ToString();
        }

        private static StringBuilder AddCommandText(this StringBuilder sb, Command command)
        {
            sb.Append(command.CommandText);
            if (command.Parameters != null)
            {
                foreach (CommandParameter parameter in command.Parameters)
                {
                    if (parameter.Name != null)
                    {
                        sb.Append(" -").Append(parameter.Name);
                    }

                    if (parameter.Value != null)
                    {
                        // This isn't going to get PowerShell's string form of the value,
                        // but it's good enough, and not as complex or expensive
                        sb.Append(' ').Append(parameter.Value);
                    }
                }
            }

            return sb;
        }
    }
}
