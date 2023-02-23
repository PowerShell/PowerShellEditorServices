// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class PSCommandHelpers
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
            if (psCommand.Commands.Count > 0)
            {
                // We need to do merge errors and output before rendering with an Out- cmdlet
                Command lastCommand = psCommand.Commands[psCommand.Commands.Count - 1];
                lastCommand.MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
            }
            return psCommand;
        }

        public static PSCommand AddProfileLoadIfExists(this PSCommand psCommand, PSObject profileVariable, string profileName, string profilePath)
        {
            // This path should be added regardless of the existence of the file.
            profileVariable.Members.Add(new PSNoteProperty(profileName, profilePath));

            if (File.Exists(profilePath))
            {
                psCommand.AddCommand(profilePath, useLocalScope: false).AddOutputCommand().AddStatement();
            }

            return psCommand;
        }

        /// <summary>
        /// Get a representation of the PSCommand, for logging purposes.
        /// </summary>
        /// <param name="command"></param>
        public static string GetInvocationText(this PSCommand command)
        {
            Command currentCommand = command.Commands[0];
            StringBuilder sb = new StringBuilder().AddCommandText(command.Commands[0]);

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

        public static string EscapeScriptFilePath(string f) => string.Concat("'", f.Replace("'", "''"), "'");

        public static PSCommand BuildDotSourceCommandWithArguments(string command, IEnumerable<string> arguments)
        {
            string args = string.Join(" ", arguments ?? Array.Empty<string>());
            string script = string.Concat(". ", command, string.IsNullOrEmpty(args) ? "" : " ", args);
            // HACK: We use AddScript instead of AddArgument/AddParameter to reuse Powershell parameter binding logic.
            return new PSCommand().AddScript(script);
        }
    }
}
