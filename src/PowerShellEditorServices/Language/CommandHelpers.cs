using System.Linq;

namespace Microsoft.PowerShell.EditorServices
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    public class CommandHelpers
    {
        public static CommandInfo GetCommandInfo(
            string commandName, 
            Runspace runspace)
        {
            CommandInfo commandInfo = null;

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.Runspace = runspace;
                powerShell.AddCommand("Get-Command");
                powerShell.AddArgument(commandName);
                commandInfo = powerShell.Invoke<CommandInfo>().FirstOrDefault();
            }

            return commandInfo;
        }

        public static string GetCommandSynopsis(
            CommandInfo commandInfo, 
            Runspace runspace)
        {
            string synopsisString = string.Empty;

            PSObject helpObject = null;

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.Runspace = runspace;
                powerShell.AddCommand("Get-Help");
                powerShell.AddArgument(commandInfo);
                helpObject = powerShell.Invoke<PSObject>().FirstOrDefault();
            }

            // Extract the synopsis string from the object
            synopsisString = 
                (string)helpObject.Properties["synopsis"].Value ?? 
                string.Empty;

            // Ignore the placeholder value for this field
            if (string.Equals(synopsisString, "SHORT DESCRIPTION", System.StringComparison.InvariantCultureIgnoreCase))
            {
                synopsisString = string.Empty;
            }

            return synopsisString;
        }
    }
}
