using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHostUserInterface : PSHostUserInterface
    {
        private readonly ILogger _logger;

        private readonly ConsoleReadLine _readLine;

        private readonly PSHostUserInterface _underlyingHostUI;

        public EditorServicesConsolePSHostUserInterface(
            ILogger logger,
            EditorServicesConsolePSHostRawUserInterface rawUI,
            ConsoleReadLine readLine,
            PSHostUserInterface underlyingHostUI)
        {
            _logger = logger;
            _readLine = readLine;
            _underlyingHostUI = underlyingHostUI;
            RawUI = rawUI;
        }

        public override PSHostRawUserInterface RawUI { get; }

        public override bool SupportsVirtualTerminal => _underlyingHostUI.SupportsVirtualTerminal;

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
        {
            return _underlyingHostUI.Prompt(caption, message, descriptions);
        }

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            return _underlyingHostUI.PromptForChoice(caption, message, choices, defaultChoice);
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            return _underlyingHostUI.PromptForCredential(caption, message, userName, targetName, allowedCredentialTypes, options);
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
        {
            return _underlyingHostUI.PromptForCredential(caption, message, userName, targetName);
        }

        public override string ReadLine()
        {
            return _readLine.ReadCommandLineAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override SecureString ReadLineAsSecureString()
        {
            return _readLine.ReadSecureLineAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
            _underlyingHostUI.Write(foregroundColor, backgroundColor, value);
        }

        public override void Write(string value)
        {
            _underlyingHostUI.Write(value);
        }

        public override void WriteDebugLine(string message)
        {
            _underlyingHostUI.WriteDebugLine(message);
        }

        public override void WriteErrorLine(string value)
        {
            _underlyingHostUI.WriteErrorLine(value);
        }

        public override void WriteLine(string value)
        {
            _underlyingHostUI.WriteLine(value);
        }

        public override void WriteProgress(long sourceId, ProgressRecord record) => _underlyingHostUI.WriteProgress(sourceId, record);

        public override void WriteVerboseLine(string message)
        {
            _underlyingHostUI.WriteVerboseLine(message);
        }

        public override void WriteWarningLine(string message)
        {
            _underlyingHostUI.WriteWarningLine(message);
        }
    }
}
