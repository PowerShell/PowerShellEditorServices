using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using PowerShellEditorServices.Services.PowerShell.Host;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHostUserInterface : PSHostUserInterface
    {
        private readonly ILogger _logger;

        private readonly PowerShellExecutionService _executionService;

        private readonly PSHostUserInterface _underlyingHostUI;

        public EditorServicesConsolePSHostUserInterface(
            ILogger logger,
            EditorServicesConsolePSHostRawUserInterface rawUI,
            PowerShellExecutionService executionService,
            PSHostUserInterface underlyingHostUI,
            bool supportsVirtualTerminal)
        {
            _logger = logger;
            _executionService = executionService;
            _underlyingHostUI = underlyingHostUI;
            RawUI = rawUI;
            SupportsVirtualTerminal = supportsVirtualTerminal;
        }

        public override PSHostRawUserInterface RawUI { get; }

        public override bool SupportsVirtualTerminal { get; }

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
        {
            throw new NotImplementedException();
        }

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
        {
            throw new NotImplementedException();
        }

        public override string ReadLine()
        {
            throw new NotImplementedException();
        }

        public override SecureString ReadLineAsSecureString()
        {
            throw new NotImplementedException();
        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) =>
            WriteOutput(value, foregroundColor, backgroundColor, includeNewline: false);

        public override void Write(string value)
        {
            throw new NotImplementedException();
        }

        public override void WriteDebugLine(string message)
        {
            throw new NotImplementedException();
        }

        public override void WriteErrorLine(string value)
        {
            throw new NotImplementedException();
        }

        public override void WriteLine(string value)
        {
            throw new NotImplementedException();
        }

        public override void WriteProgress(long sourceId, ProgressRecord record) => _underlyingHostUI.WriteProgress(sourceId, record);

        public override void WriteVerboseLine(string message)
        {
            throw new NotImplementedException();
        }

        public override void WriteWarningLine(string message)
        {
            throw new NotImplementedException();
        }

        private void WriteOutput(
            string outputString,
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor,
            bool includeNewline)
        {
            ConsoleColor oldForegroundColor = System.Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = System.Console.BackgroundColor;

            System.Console.ForegroundColor = foregroundColor;
            System.Console.BackgroundColor = backgroundColor;

            if (includeNewline)
            {
                System.Console.WriteLine(outputString);
            }
            else
            {
                System.Console.Write(outputString);
            }

            System.Console.ForegroundColor = oldForegroundColor;
            System.Console.BackgroundColor = oldBackgroundColor;
        }
    }
}
