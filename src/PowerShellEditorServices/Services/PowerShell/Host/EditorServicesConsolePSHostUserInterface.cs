// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHostUserInterface : PSHostUserInterface, IHostUISupportsMultipleChoiceSelection
    {
        private readonly PSHostUserInterface _underlyingHostUI;

        /// <summary>
        /// We use a ConcurrentDictionary because ConcurrentHashSet does not exist, hence the value
        /// is never actually used, and `WriteProgress` must be thread-safe.
        /// </summary>
        private readonly ConcurrentDictionary<(long, int), object> _currentProgressRecords = new();

        public EditorServicesConsolePSHostUserInterface(
            ILoggerFactory loggerFactory,
            PSHostUserInterface underlyingHostUI)
        {
            _underlyingHostUI = underlyingHostUI;
            RawUI = new EditorServicesConsolePSHostRawUserInterface(loggerFactory, underlyingHostUI.RawUI);
        }

        public override bool SupportsVirtualTerminal => _underlyingHostUI.SupportsVirtualTerminal;

        public override PSHostRawUserInterface RawUI { get; }

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions) => _underlyingHostUI.Prompt(caption, message, descriptions);

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice) => _underlyingHostUI.PromptForChoice(caption, message, choices, defaultChoice);

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options) => _underlyingHostUI.PromptForCredential(caption, message, userName, targetName, allowedCredentialTypes, options);

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName) => _underlyingHostUI.PromptForCredential(caption, message, userName, targetName);

        public override string ReadLine() => _underlyingHostUI.ReadLine();

        public override SecureString ReadLineAsSecureString() => _underlyingHostUI.ReadLineAsSecureString();

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) => _underlyingHostUI.Write(foregroundColor, backgroundColor, value);

        public override void Write(string value) => _underlyingHostUI.Write(value);

        public override void WriteDebugLine(string message) => _underlyingHostUI.WriteDebugLine(message);

        public override void WriteErrorLine(string value) => _underlyingHostUI.WriteErrorLine(value);

        public override void WriteInformation(InformationRecord record) => _underlyingHostUI.WriteInformation(record);

        public override void WriteLine() => _underlyingHostUI.WriteLine();

        public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) => _underlyingHostUI.WriteLine(foregroundColor, backgroundColor, value);

        public override void WriteLine(string value) => _underlyingHostUI.WriteLine(value);

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
            _ = record.RecordType == ProgressRecordType.Completed
                ? _currentProgressRecords.TryRemove((sourceId, record.ActivityId), out _)
                : _currentProgressRecords.TryAdd((sourceId, record.ActivityId), null);
            _underlyingHostUI.WriteProgress(sourceId, record);
        }

        public void ResetProgress()
        {
            // Mark all processed progress records as completed.
            foreach ((long sourceId, int activityId) in _currentProgressRecords.Keys)
            {
                // NOTE: This initializer checks that string is not null nor empty, so it must have
                // some text in it.
                ProgressRecord record = new(activityId, "0", "0")
                {
                    RecordType = ProgressRecordType.Completed
                };
                _underlyingHostUI.WriteProgress(sourceId, record);
                _currentProgressRecords.Clear();
            }
            // TODO: Maybe send the OSC sequence to turn off progress indicator.
        }

        public override void WriteVerboseLine(string message) => _underlyingHostUI.WriteVerboseLine(message);

        public override void WriteWarningLine(string message) => _underlyingHostUI.WriteWarningLine(message);

        public Collection<int> PromptForChoice(
            string caption,
            string message,
            Collection<ChoiceDescription> choices,
            IEnumerable<int> defaultChoices)
            => ((IHostUISupportsMultipleChoiceSelection)_underlyingHostUI).PromptForChoice(caption, message, choices, defaultChoices);
    }
}
