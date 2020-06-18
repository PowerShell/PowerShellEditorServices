using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class PSCommandExtensions
    {
        public static string GetInvocationText(this PSCommand command)
        {
            Command lastCommand = command.Commands[0];
            var sb = new StringBuilder().AddCommandText(command.Commands[0]);

            for (int i = 1; i < command.Commands.Count; i++)
            {
                sb.Append(lastCommand.IsEndOfStatement ? "; " : " | ");
                lastCommand = command.Commands[i];
                sb.AddCommandText(lastCommand);
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
