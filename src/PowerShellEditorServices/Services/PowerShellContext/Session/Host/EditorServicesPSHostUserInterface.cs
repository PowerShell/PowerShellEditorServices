//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides an implementation of the PSHostUserInterface class
    /// for the ConsoleService and routes its calls to an IConsoleHost
    /// implementation.
    /// </summary>
    internal abstract class EditorServicesPSHostUserInterface :
        PSHostUserInterface,
        IHostInput,
        IHostOutput,
        IHostUISupportsMultipleChoiceSelection
    {
        #region Private Fields

        private readonly ConcurrentDictionary<ProgressKey, object> currentProgressMessages =
            new ConcurrentDictionary<ProgressKey, object>();

        private PromptHandler activePromptHandler;
        private PSHostRawUserInterface rawUserInterface;
        private CancellationTokenSource commandLoopCancellationToken;

        /// <summary>
        /// The PowerShellContext to use for executing commands.
        /// </summary>
        protected PowerShellContextService powerShellContext;

        #endregion

        #region Public Constants

        /// <summary>
        /// Gets a const string for the console's debug message prefix.
        /// </summary>
        public const string DebugMessagePrefix = "DEBUG: ";

        /// <summary>
        /// Gets a const string for the console's warning message prefix.
        /// </summary>
        public const string WarningMessagePrefix = "WARNING: ";

        /// <summary>
        /// Gets a const string for the console's verbose message prefix.
        /// </summary>
        public const string VerboseMessagePrefix = "VERBOSE: ";

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the host supports VT100 output codes.
        /// </summary>
        public override bool SupportsVirtualTerminal => false;

        /// <summary>
        /// Returns true if a native application is currently running.
        /// </summary>
        public bool IsNativeApplicationRunning { get; internal set; }

        private bool IsCommandLoopRunning { get; set; }

        /// <summary>
        /// Gets the ILogger implementation used for this host.
        /// </summary>
        protected ILogger Logger { get; private set; }

        /// <summary>
        /// Gets a value indicating whether writing progress is supported.
        /// </summary>
        internal protected virtual bool SupportsWriteProgress => false;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHostUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="powerShellContext">The PowerShellContext to use for executing commands.</param>
        /// <param name="rawUserInterface">The PSHostRawUserInterface implementation to use for this host.</param>
        /// <param name="logger">An ILogger implementation to use for this host.</param>
        public EditorServicesPSHostUserInterface(
            PowerShellContextService powerShellContext,
            PSHostRawUserInterface rawUserInterface,
            ILogger logger)
        {
            this.Logger = logger;
            this.powerShellContext = powerShellContext;
            this.rawUserInterface = rawUserInterface;

            this.powerShellContext.DebuggerStop += PowerShellContext_DebuggerStop;
            this.powerShellContext.DebuggerResumed += PowerShellContext_DebuggerResumed;
            this.powerShellContext.ExecutionStatusChanged += PowerShellContext_ExecutionStatusChanged;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the host's interactive command loop.
        /// </summary>
        public void StartCommandLoop()
        {
            if (!this.IsCommandLoopRunning)
            {
                this.IsCommandLoopRunning = true;
                this.ShowCommandPrompt();
            }
        }

        /// <summary>
        /// Stops the host's interactive command loop.
        /// </summary>
        public void StopCommandLoop()
        {
            if (this.IsCommandLoopRunning)
            {
                this.IsCommandLoopRunning = false;
                this.CancelCommandPrompt();
            }
        }

        private void ShowCommandPrompt()
        {
            if (this.commandLoopCancellationToken == null)
            {
                this.commandLoopCancellationToken = new CancellationTokenSource();
                Task.Run(() => this.StartReplLoopAsync(this.commandLoopCancellationToken.Token));
            }
            else
            {
                Logger.LogTrace("StartReadLoop called while read loop is already running");
            }
        }

        private void CancelCommandPrompt()
        {
            if (this.commandLoopCancellationToken != null)
            {
                // Set this to false so that Ctrl+C isn't trapped by any
                // lingering ReadKey
                // TOOD: Move this to Terminal impl!
                //Console.TreatControlCAsInput = false;

                this.commandLoopCancellationToken.Cancel();
                this.commandLoopCancellationToken = null;
            }
        }

        /// <summary>
        /// Cancels the currently executing command or prompt.
        /// </summary>
        public void SendControlC()
        {
            if (this.activePromptHandler != null)
            {
                this.activePromptHandler.CancelPrompt();
            }
            else
            {
                // Cancel the current execution
                this.powerShellContext.AbortExecution();
            }
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Requests that the HostUI implementation read a command line
        /// from the user to be executed in the integrated console command
        /// loop.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken used to cancel the command line request.
        /// </param>
        /// <returns>A Task that can be awaited for the resulting input string.</returns>
        protected abstract Task<string> ReadCommandLineAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Creates an InputPrompt handle to use for displaying input
        /// prompts to the user.
        /// </summary>
        /// <returns>A new InputPromptHandler instance.</returns>
        protected abstract InputPromptHandler OnCreateInputPromptHandler();

        /// <summary>
        /// Creates a ChoicePromptHandler to use for displaying a
        /// choice prompt to the user.
        /// </summary>
        /// <returns>A new ChoicePromptHandler instance.</returns>
        protected abstract ChoicePromptHandler OnCreateChoicePromptHandler();

        /// <summary>
        /// Writes output of the given type to the user interface with
        /// the given foreground and background colors.  Also includes
        /// a newline if requested.
        /// </summary>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="includeNewLine">
        /// If true, a newline should be appended to the output's contents.
        /// </param>
        /// <param name="outputType">
        /// Specifies the type of output to be written.
        /// </param>
        /// <param name="foregroundColor">
        /// Specifies the foreground color of the output to be written.
        /// </param>
        /// <param name="backgroundColor">
        /// Specifies the background color of the output to be written.
        /// </param>
        public abstract void WriteOutput(
            string outputString,
            bool includeNewLine,
            OutputType outputType,
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor);

        /// <summary>
        /// Sends a progress update event to the user.
        /// </summary>
        /// <param name="sourceId">The source ID of the progress event.</param>
        /// <param name="progressDetails">The details of the activity's current progress.</param>
        protected abstract void UpdateProgress(
            long sourceId,
            ProgressDetails progressDetails);

        #endregion

        #region IHostInput Implementation

        #endregion

        #region PSHostUserInterface Implementation

        /// <summary>
        ///
        /// </summary>
        /// <param name="promptCaption"></param>
        /// <param name="promptMessage"></param>
        /// <param name="fieldDescriptions"></param>
        /// <returns></returns>
        public override Dictionary<string, PSObject> Prompt(
            string promptCaption,
            string promptMessage,
            Collection<FieldDescription> fieldDescriptions)
        {
            FieldDetails[] fields =
                fieldDescriptions
                    .Select(f => { return FieldDetails.Create(f, this.Logger); })
                    .ToArray();

            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            Task<Dictionary<string, object>> promptTask =
                this.CreateInputPromptHandler()
                    .PromptForInputAsync(
                        promptCaption,
                        promptMessage,
                        fields,
                        cancellationToken.Token);

            // Run the prompt task and wait for it to return
            this.WaitForPromptCompletion(
                promptTask,
                "Prompt",
                cancellationToken);

            // Convert all values to PSObjects
            var psObjectDict = new Dictionary<string, PSObject>();

            // The result will be null if the prompt was cancelled
            if (promptTask.Result != null)
            {
                // Convert all values to PSObjects
                foreach (var keyValuePair in promptTask.Result)
                {
                    psObjectDict.Add(
                        keyValuePair.Key,
                        PSObject.AsPSObject(keyValuePair.Value ?? string.Empty));
                }
            }

            // Return the result
            return psObjectDict;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="promptCaption"></param>
        /// <param name="promptMessage"></param>
        /// <param name="choiceDescriptions"></param>
        /// <param name="defaultChoice"></param>
        /// <returns></returns>
        public override int PromptForChoice(
            string promptCaption,
            string promptMessage,
            Collection<ChoiceDescription> choiceDescriptions,
            int defaultChoice)
        {
            ChoiceDetails[] choices =
                choiceDescriptions
                    .Select(ChoiceDetails.Create)
                    .ToArray();

            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            Task<int> promptTask =
                this.CreateChoicePromptHandler()
                    .PromptForChoiceAsync(
                        promptCaption,
                        promptMessage,
                        choices,
                        defaultChoice,
                        cancellationToken.Token);

            // Run the prompt task and wait for it to return
            this.WaitForPromptCompletion(
                promptTask,
                "PromptForChoice",
                cancellationToken);

            // Return the result
            return promptTask.Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="promptCaption"></param>
        /// <param name="promptMessage"></param>
        /// <param name="userName"></param>
        /// <param name="targetName"></param>
        /// <param name="allowedCredentialTypes"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override PSCredential PromptForCredential(
            string promptCaption,
            string promptMessage,
            string userName,
            string targetName,
            PSCredentialTypes allowedCredentialTypes,
            PSCredentialUIOptions options)
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();

            Task<Dictionary<string, object>> promptTask =
                this.CreateInputPromptHandler()
                    .PromptForInputAsync(
                        promptCaption,
                        promptMessage,
                        new FieldDetails[] { new CredentialFieldDetails("Credential", "Credential", userName) },
                        cancellationToken.Token);

            Task<PSCredential> unpackTask =
                promptTask.ContinueWith(
                    task =>
                    {
                        if (task.IsFaulted)
                        {
                            throw task.Exception;
                        }
                        else if (task.IsCanceled)
                        {
                            throw new TaskCanceledException(task);
                        }

                        // Return the value of the sole field
                        return (PSCredential)task.Result?["Credential"];
                    });

            // Run the prompt task and wait for it to return
            this.WaitForPromptCompletion(
                unpackTask,
                "PromptForCredential",
                cancellationToken);

            return unpackTask.Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="caption"></param>
        /// <param name="message"></param>
        /// <param name="userName"></param>
        /// <param name="targetName"></param>
        /// <returns></returns>
        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName)
        {
            return this.PromptForCredential(
                caption,
                message,
                userName,
                targetName,
                PSCredentialTypes.Default,
                PSCredentialUIOptions.Default);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public override PSHostRawUserInterface RawUI
        {
            get { return this.rawUserInterface; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public override string ReadLine()
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();

            Task<string> promptTask =
                this.CreateInputPromptHandler()
                    .PromptForInputAsync(cancellationToken.Token);

            // Run the prompt task and wait for it to return
            this.WaitForPromptCompletion(
                promptTask,
                "ReadLine",
                cancellationToken);

            return promptTask.Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public override SecureString ReadLineAsSecureString()
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();

            Task<SecureString> promptTask =
                this.CreateInputPromptHandler()
                    .PromptForSecureInputAsync(cancellationToken.Token);

            // Run the prompt task and wait for it to return
            this.WaitForPromptCompletion(
                promptTask,
                "ReadLineAsSecureString",
                cancellationToken);

            return promptTask.Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="foregroundColor"></param>
        /// <param name="backgroundColor"></param>
        /// <param name="value"></param>
        public override void Write(
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor,
            string value)
        {
            this.WriteOutput(
                value,
                false,
                OutputType.Normal,
                foregroundColor,
                backgroundColor);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public override void Write(string value)
        {
            this.WriteOutput(
                value,
                false,
                OutputType.Normal,
                this.rawUserInterface.ForegroundColor,
                this.rawUserInterface.BackgroundColor);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public override void WriteLine(string value)
        {
            this.WriteOutput(
                value,
                true,
                OutputType.Normal,
                this.rawUserInterface.ForegroundColor,
                this.rawUserInterface.BackgroundColor);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public override void WriteDebugLine(string message)
        {
            this.WriteOutput(
                DebugMessagePrefix + message,
                true,
                OutputType.Debug,
                foregroundColor: this.DebugForegroundColor,
                backgroundColor: this.DebugBackgroundColor);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public override void WriteVerboseLine(string message)
        {
            this.WriteOutput(
                VerboseMessagePrefix + message,
                true,
                OutputType.Verbose,
                foregroundColor: this.VerboseForegroundColor,
                backgroundColor: this.VerboseBackgroundColor);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public override void WriteWarningLine(string message)
        {
            this.WriteOutput(
                WarningMessagePrefix + message,
                true,
                OutputType.Warning,
                foregroundColor: this.WarningForegroundColor,
                backgroundColor: this.WarningBackgroundColor);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public override void WriteErrorLine(string value)
        {
            // PowerShell's ConsoleHost also skips over empty lines:
            // https://github.com/PowerShell/PowerShell/blob/8e683972284a5a7f773ea6d027d9aac14d7e7524/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHostUserInterface.cs#L1334-L1337
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            this.WriteOutput(
                value,
                true,
                OutputType.Error,
                foregroundColor: this.ErrorForegroundColor,
                backgroundColor: this.ErrorBackgroundColor);
        }

        /// <summary>
        /// Invoked by <see cref="Cmdlet.WriteProgress(ProgressRecord)" /> to display a progress record.
        /// </summary>
        /// <param name="sourceId">
        /// Unique identifier of the source of the record. An int64 is used because typically,
        /// the 'this' pointer of the command from whence the record is originating is used, and
        /// that may be from a remote Runspace on a 64-bit machine.
        /// </param>
        /// <param name="record">
        /// The record being reported to the host.
        /// </param>
        public sealed override void WriteProgress(
            long sourceId,
            ProgressRecord record)
        {
            // Maintain old behavior if this isn't overridden.
            if (!this.SupportsWriteProgress)
            {
                this.UpdateProgress(sourceId, ProgressDetails.Create(record));
                return;
            }

            // Keep a list of progress records we write so we can automatically
            // clean them up after the pipeline ends.
            if (record.RecordType == ProgressRecordType.Completed)
            {
                this.currentProgressMessages.TryRemove(new ProgressKey(sourceId, record), out _);
            }
            else
            {
                // Adding with a value of null here because we don't actually need a dictionary. We're
                // only using ConcurrentDictionary<,> becuase there is no ConcurrentHashSet<>.
                this.currentProgressMessages.TryAdd(new ProgressKey(sourceId, record), null);
            }

            this.WriteProgressImpl(sourceId, record);
        }

        /// <summary>
        /// Invoked by <see cref="Cmdlet.WriteProgress(ProgressRecord)" /> to display a progress record.
        /// </summary>
        /// <param name="sourceId">
        /// Unique identifier of the source of the record. An int64 is used because typically,
        /// the 'this' pointer of the command from whence the record is originating is used, and
        /// that may be from a remote Runspace on a 64-bit machine.
        /// </param>
        /// <param name="record">
        /// The record being reported to the host.
        /// </param>
        protected virtual void WriteProgressImpl(long sourceId, ProgressRecord record)
        {
        }

        internal void ClearProgress()
        {
            const string nonEmptyString = "noop";
            if (!this.SupportsWriteProgress)
            {
                return;
            }

            foreach (ProgressKey key in this.currentProgressMessages.Keys)
            {
                // This constructor throws if the activity description is empty even
                // with completed records.
                var record = new ProgressRecord(
                    key.ActivityId,
                    activity: nonEmptyString,
                    statusDescription: nonEmptyString);

                record.RecordType = ProgressRecordType.Completed;
                this.WriteProgressImpl(key.SourceId, record);
            }

            this.currentProgressMessages.Clear();
        }

        #endregion

        #region IHostUISupportsMultipleChoiceSelection Implementation

        /// <summary>
        ///
        /// </summary>
        /// <param name="promptCaption"></param>
        /// <param name="promptMessage"></param>
        /// <param name="choiceDescriptions"></param>
        /// <param name="defaultChoices"></param>
        /// <returns></returns>
        public Collection<int> PromptForChoice(
            string promptCaption,
            string promptMessage,
            Collection<ChoiceDescription> choiceDescriptions,
            IEnumerable<int> defaultChoices)
        {
            ChoiceDetails[] choices =
                choiceDescriptions
                    .Select(ChoiceDetails.Create)
                    .ToArray();

            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            Task<int[]> promptTask =
                this.CreateChoicePromptHandler()
                    .PromptForChoiceAsync(
                        promptCaption,
                        promptMessage,
                        choices,
                        defaultChoices.ToArray(),
                        cancellationToken.Token);

            // Run the prompt task and wait for it to return
            this.WaitForPromptCompletion(
                promptTask,
                "PromptForChoice",
                cancellationToken);

            // Return the result
            return new Collection<int>(promptTask.Result.ToList());
        }

        #endregion

        #region Private Methods

        private Coordinates lastPromptLocation;

        private async Task WritePromptStringToHostAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this.lastPromptLocation != null &&
                    this.lastPromptLocation.X == await ConsoleProxy.GetCursorLeftAsync(cancellationToken).ConfigureAwait(false) &&
                    this.lastPromptLocation.Y == await ConsoleProxy.GetCursorTopAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            // When output is redirected (like when running tests) attempting to get
            // the cursor position will throw.
            catch (System.IO.IOException)
            {
            }

            PSCommand promptCommand = new PSCommand().AddCommand("prompt");

            cancellationToken.ThrowIfCancellationRequested();
            string promptString =
                (await this.powerShellContext.ExecuteCommandAsync<PSObject>(promptCommand, false, false).ConfigureAwait(false))
                    .Select(pso => pso.BaseObject)
                    .OfType<string>()
                    .FirstOrDefault() ?? "PS> ";

            // Add the [DBG] prefix if we're stopped in the debugger and the prompt doesn't already have [DBG] in it
            if (this.powerShellContext.IsDebuggerStopped && !promptString.Contains("[DBG]"))
            {
                promptString =
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "[DBG]: {0}",
                        promptString);
            }

            // Update the stored prompt string if the session is remote
            if (this.powerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
            {
                promptString =
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "[{0}]: {1}",
                        this.powerShellContext.CurrentRunspace.Runspace.ConnectionInfo != null
                            ? this.powerShellContext.CurrentRunspace.Runspace.ConnectionInfo.ComputerName
                            : this.powerShellContext.CurrentRunspace.SessionDetails.ComputerName,
                        promptString);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Write the prompt string
            this.WriteOutput(promptString, false);
            this.lastPromptLocation = new Coordinates(
                await ConsoleProxy.GetCursorLeftAsync(cancellationToken).ConfigureAwait(false),
                await ConsoleProxy.GetCursorTopAsync(cancellationToken).ConfigureAwait(false));
        }

        private void WriteDebuggerBanner(DebuggerStopEventArgs eventArgs)
        {
            // TODO: What do we display when we don't know why we stopped?

            if (eventArgs.Breakpoints.Count > 0)
            {
                // The breakpoint classes have nice ToString output so use that
                this.WriteOutput(
                    Environment.NewLine + $"Hit {eventArgs.Breakpoints[0].ToString()}\n",
                    true,
                    OutputType.Normal,
                    ConsoleColor.Blue);
            }
        }

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

        private async Task StartReplLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string commandString = null;

                try
                {
                    await this.WritePromptStringToHostAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    commandString = await this.ReadCommandLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (PipelineStoppedException)
                {
                    this.WriteOutput(
                        "^C",
                        true,
                        OutputType.Normal,
                        foregroundColor: ConsoleColor.Red);
                }
                // Do nothing here, the while loop condition will exit.
                catch (TaskCanceledException)
                { }
                catch (OperationCanceledException)
                { }
                catch (Exception e) // Narrow this if possible
                {
                    this.WriteOutput(
                        $"\n\nAn error occurred while reading input:\n\n{e.ToString()}\n",
                        true,
                        OutputType.Error);

                    Logger.LogException("Caught exception while reading command line", e);
                }
                finally
                {
                    // This supplies the newline in the Legacy ReadLine when executing code in the terminal via hitting the ENTER key
                    // or Ctrl+C. Without this, hitting ENTER with a no input looks like it does nothing (no new prompt is written)
                    // and also the output would show up on the same line as the code you wanted to execute (the prompt line).
                    // This is AlSO applied to PSReadLine for the Ctrl+C scenario which appears like it does nothing...
                    // TODO: This still gives an extra newline when you hit ENTER in the PSReadLine experience. We should figure
                    // out if there's any way to avoid that... but unfortunately, in both scenarios, we only see that empty
                    // string is returned.
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        this.WriteLine();
                    }
                }

                if (!string.IsNullOrWhiteSpace(commandString))
                {
                    var unusedTask =
                        this.powerShellContext
                            .ExecuteScriptStringAsync(
                                commandString,
                                writeInputToHost: false,
                                writeOutputToHost: true,
                                addToHistory: true)
                            .ConfigureAwait(continueOnCapturedContext: false);

                    break;
                }
            }
        }

        private InputPromptHandler CreateInputPromptHandler()
        {
            if (this.activePromptHandler != null)
            {
                Logger.LogError(
                    "Prompt handler requested while another prompt is already active.");
            }

            InputPromptHandler inputPromptHandler = this.OnCreateInputPromptHandler();
            this.activePromptHandler = inputPromptHandler;
            this.activePromptHandler.PromptCancelled += activePromptHandler_PromptCancelled;

            return inputPromptHandler;
        }

        private ChoicePromptHandler CreateChoicePromptHandler()
        {
            if (this.activePromptHandler != null)
            {
                Logger.LogError(
                    "Prompt handler requested while another prompt is already active.");
            }

            ChoicePromptHandler choicePromptHandler = this.OnCreateChoicePromptHandler();
            this.activePromptHandler = choicePromptHandler;
            this.activePromptHandler.PromptCancelled += activePromptHandler_PromptCancelled;

            return choicePromptHandler;
        }

        private void activePromptHandler_PromptCancelled(object sender, EventArgs e)
        {
            // Clean up the existing prompt
            this.activePromptHandler.PromptCancelled -= activePromptHandler_PromptCancelled;
            this.activePromptHandler = null;
        }
        private void WaitForPromptCompletion<TResult>(
            Task<TResult> promptTask,
            string promptFunctionName,
            CancellationTokenSource cancellationToken)
        {
            try
            {
                // This will synchronously block on the prompt task
                // method which gets run on another thread.
                promptTask.Wait();

                if (promptTask.Status == TaskStatus.WaitingForActivation)
                {
                    // The Wait() call has timed out, cancel the prompt
                    cancellationToken.Cancel();

                    this.WriteOutput("\r\nPrompt has been cancelled due to a timeout.\r\n");
                    throw new PipelineStoppedException();
                }
            }
            catch (AggregateException e)
            {
                // Find the right InnerException
                Exception innerException = e.InnerException;
                while (innerException is AggregateException)
                {
                    innerException = innerException.InnerException;
                }

                // Was the task cancelled?
                if (innerException is TaskCanceledException)
                {
                    // Stop the pipeline if the prompt was cancelled
                    throw new PipelineStoppedException();
                }
                else if (innerException is PipelineStoppedException)
                {
                    // The prompt is being cancelled, rethrow the exception
                    throw innerException;
                }
                else
                {
                    // Rethrow the exception
                    throw new Exception(
                        string.Format(
                            "{0} failed, check inner exception for details",
                            promptFunctionName),
                        innerException);
                }
            }
        }

        private void PowerShellContext_DebuggerStop(object sender, System.Management.Automation.DebuggerStopEventArgs e)
        {
            if (!this.IsCommandLoopRunning)
            {
                StartCommandLoop();
                return;
            }

            // Cancel any existing prompt first
            this.CancelCommandPrompt();

            this.WriteDebuggerBanner(e);
            this.ShowCommandPrompt();
        }

        private void PowerShellContext_DebuggerResumed(object sender, System.Management.Automation.DebuggerResumeAction e)
        {
            this.CancelCommandPrompt();
        }

        private void PowerShellContext_ExecutionStatusChanged(object sender, ExecutionStatusChangedEventArgs eventArgs)
        {
            // The command loop should only be manipulated if it's already started
            if (eventArgs.ExecutionStatus == ExecutionStatus.Aborted)
            {
                this.ClearProgress();

                // When aborted, cancel any lingering prompts
                if (this.activePromptHandler != null)
                {
                    this.activePromptHandler.CancelPrompt();
                    this.WriteOutput(string.Empty);
                }
            }
            else if (
                eventArgs.ExecutionOptions.WriteOutputToHost ||
                eventArgs.ExecutionOptions.InterruptCommandPrompt)
            {
                // Any command which writes output to the host will affect
                // the display of the prompt
                if (eventArgs.ExecutionStatus != ExecutionStatus.Running)
                {
                    this.ClearProgress();

                    // Execution has completed, start the input prompt
                    this.ShowCommandPrompt();
                    StartCommandLoop();
                }
                else
                {
                    // A new command was started, cancel the input prompt
                    StopCommandLoop();
                    this.CancelCommandPrompt();
                }
            }
            else if (
                eventArgs.ExecutionOptions.WriteErrorsToHost &&
                (eventArgs.ExecutionStatus == ExecutionStatus.Failed ||
                    eventArgs.HadErrors))
            {
                this.ClearProgress();
                this.WriteOutput(string.Empty, true);
                var unusedTask = this.WritePromptStringToHostAsync(CancellationToken.None);
            }
        }

        #endregion

        private readonly struct ProgressKey : IEquatable<ProgressKey>
        {
            internal readonly long SourceId;

            internal readonly int ActivityId;

            internal readonly int ParentActivityId;

            internal ProgressKey(long sourceId, ProgressRecord record)
            {
                SourceId = sourceId;
                ActivityId = record.ActivityId;
                ParentActivityId = record.ParentActivityId;
            }

            public bool Equals(ProgressKey other)
            {
                return SourceId == other.SourceId
                    && ActivityId == other.ActivityId
                    && ParentActivityId == other.ParentActivityId;
            }

            public override int GetHashCode()
            {
                // Algorithm from https://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + SourceId.GetHashCode();
                    hash = hash * 31 + ActivityId.GetHashCode();
                    hash = hash * 31 + ParentActivityId.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
