using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Reflection;
using System.Security;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHostUserInterface : PSHostUserInterface
    {
        private readonly ILogger _logger;

        private readonly IReadLineProvider _readLineProvider;

        private readonly PSHostUserInterface _underlyingHostUI;

        private readonly PSHostUserInterface _consoleHostUI;

        public EditorServicesConsolePSHostUserInterface(
            ILoggerFactory loggerFactory,
            IReadLineProvider readLineProvider,
            PSHostUserInterface underlyingHostUI)
        {
            _logger = loggerFactory.CreateLogger<EditorServicesConsolePSHostUserInterface>();
            _readLineProvider = readLineProvider;
            _underlyingHostUI = underlyingHostUI;
            RawUI = new EditorServicesConsolePSHostRawUserInterface(loggerFactory, underlyingHostUI.RawUI);

            _consoleHostUI = GetConsoleHostUI(_underlyingHostUI);
            if (_consoleHostUI != null)
            {
                SetConsoleHostUIToInteractive(_consoleHostUI);
            }
        }

        public override PSHostRawUserInterface RawUI { get; }

        public override bool SupportsVirtualTerminal => _underlyingHostUI.SupportsVirtualTerminal;

        #region Console color support

        internal static ConsoleColor BackgroundColor { get; set; }

        internal ConsoleColor FormatAccentColor { get; set; } = ConsoleColor.Green;
        internal ConsoleColor ErrorAccentColor { get; set; } = ConsoleColor.Cyan;

        internal ConsoleColor ErrorForegroundColor { get; set; } = ConsoleColor.Red;
        internal ConsoleColor ErrorBackgroundColor { get; set; } = BackgroundColor;

        internal ConsoleColor WarningForegroundColor { get; set; } = ConsoleColor.Yellow;
        internal ConsoleColor WarningBackgroundColor { get; set; } = BackgroundColor;

        internal ConsoleColor DebugForegroundColor { get; set; } = ConsoleColor.Yellow;
        internal ConsoleColor DebugBackgroundColor { get; set; } = BackgroundColor;

        internal ConsoleColor VerboseForegroundColor { get; set; } = ConsoleColor.Yellow;
        internal ConsoleColor VerboseBackgroundColor { get; set; } = BackgroundColor;
        internal virtual ConsoleColor ProgressForegroundColor { get; set; } = ConsoleColor.Yellow;
        internal virtual ConsoleColor ProgressBackgroundColor { get; set; } = ConsoleColor.DarkCyan;

        #endregion

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
        {
            if (_consoleHostUI != null)
            {
                return _consoleHostUI.Prompt(caption, message, descriptions);
            }

            return _underlyingHostUI.Prompt(caption, message, descriptions);
        }

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            if (_consoleHostUI != null)
            {
                return _consoleHostUI.PromptForChoice(caption, message, choices, defaultChoice);
            }

            return _underlyingHostUI.PromptForChoice(caption, message, choices, defaultChoice);
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            if (_consoleHostUI != null)
            {
                return _consoleHostUI.PromptForCredential(caption, message, userName, targetName, allowedCredentialTypes, options);
            }

            return _underlyingHostUI.PromptForCredential(caption, message, userName, targetName, allowedCredentialTypes, options);
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
        {
            if (_consoleHostUI != null)
            {
                return _consoleHostUI.PromptForCredential(caption, message, userName, targetName);
            }

            return _underlyingHostUI.PromptForCredential(caption, message, userName, targetName);
        }

        public override string ReadLine()
        {
            return _readLineProvider.ReadLine.ReadLine(CancellationToken.None);
        }

        public override SecureString ReadLineAsSecureString()
        {
            return _readLineProvider.ReadLine.ReadSecureLine(CancellationToken.None);
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

        private static PSHostUserInterface GetConsoleHostUI(PSHostUserInterface ui)
        {
            FieldInfo externalUIField = ui.GetType().GetField("_externalUI", BindingFlags.NonPublic | BindingFlags.Instance);

            if (externalUIField == null)
            {
                return null;
            }

            return (PSHostUserInterface)externalUIField.GetValue(ui);
        }

        private static void SetConsoleHostUIToInteractive(PSHostUserInterface ui)
        {
            ui.GetType().GetProperty("ThrowOnReadAndPrompt", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, false);
        }
    }
}
