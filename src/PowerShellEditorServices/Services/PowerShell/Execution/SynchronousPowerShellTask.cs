// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Utility;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal class SynchronousPowerShellTask<TResult> : SynchronousTask<IReadOnlyList<TResult>>
    {
        private static readonly PowerShellExecutionOptions s_defaultPowerShellExecutionOptions = new();

        private readonly ILogger _logger;

        private readonly PsesInternalHost _psesHost;

        private readonly PSCommand _psCommand;

        private SMA.PowerShell _pwsh;

        private PowerShellContextFrame _frame;

        public SynchronousPowerShellTask(
            ILogger logger,
            PsesInternalHost psesHost,
            PSCommand command,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _logger = logger;
            _psesHost = psesHost;
            _psCommand = command;
            PowerShellExecutionOptions = executionOptions ?? s_defaultPowerShellExecutionOptions;
        }

        public PowerShellExecutionOptions PowerShellExecutionOptions { get; }

        public override ExecutionOptions ExecutionOptions => PowerShellExecutionOptions;

        // These are PowerShell's intrinsic debugger commands that must be run via
        // `ProcessDebugCommand`.
        private static readonly string[] DebuggerCommands = { "continue", "c", "k", "h", "?", "list", "l", "stepInto", "s", "stepOut", "o", "stepOver", "v", "quit", "q", "detach", "d" };

        public override IReadOnlyList<TResult> Run(CancellationToken cancellationToken)
        {
            _psesHost.Runspace.ThrowCancelledIfUnusable();
            PowerShellContextFrame frame = _psesHost.PushPowerShellForExecution();
            try
            {
                _pwsh = _psesHost.CurrentPowerShell;

                if (PowerShellExecutionOptions.WriteInputToHost)
                {
                    _psesHost.WriteWithPrompt(_psCommand, cancellationToken);
                }

                return _pwsh.Runspace.Debugger.InBreakpoint
                    && (IsDebuggerCommand(_psCommand) || _pwsh.Runspace.RunspaceIsRemote)
                    ? ExecuteInDebugger(cancellationToken)
                    : ExecuteNormally(cancellationToken);
            }
            finally
            {
                _psesHost.PopPowerShellForExecution(frame);
            }
        }

        public override string ToString() => _psCommand.GetInvocationText();

        private static bool IsDebuggerCommand(PSCommand command)
        {
            if (command.Commands.Count is not 1
                || command.Commands[0] is { IsScript: false } or { Parameters.Count: > 0 })
            {
                return false;
            }

            string commandText = command.Commands[0].CommandText;
            foreach (string knownCommand in DebuggerCommands)
            {
                if (commandText.Equals(knownCommand, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<TResult> ExecuteNormally(CancellationToken cancellationToken)
        {
            _frame = _psesHost.CurrentFrame;
            if (PowerShellExecutionOptions.WriteOutputToHost)
            {
                _psCommand.AddOutputCommand();
            }

            cancellationToken.Register(CancelNormalExecution);

            Collection<TResult> result = null;
            try
            {
                PSInvocationSettings invocationSettings = new()
                {
                    AddToHistory = PowerShellExecutionOptions.AddToHistory,
                };

                if (PowerShellExecutionOptions.ThrowOnError)
                {
                    invocationSettings.ErrorActionPreference = ActionPreference.Stop;
                }

                result = _pwsh.InvokeCommand<TResult>(_psCommand, invocationSettings);
                cancellationToken.ThrowIfCancellationRequested();
            }
            // Allow terminate exceptions to propogate for flow control.
            catch (TerminateException)
            {
                throw;
            }
            // Test if we've been cancelled. If we're remoting, PSRemotingDataStructureException
            // effectively means the pipeline was stopped.
            catch (Exception e) when (cancellationToken.IsCancellationRequested || e is PipelineStoppedException || e is PSRemotingDataStructureException)
            {
                // ExecuteNormally handles user commands in a debug session. Perhaps we should clean all this up somehow.
                if (_pwsh.Runspace.Debugger.InBreakpoint)
                {
                    StopDebuggerIfRemoteDebugSessionFailed();
                }
                throw new OperationCanceledException();
            }
            // We only catch RuntimeExceptions here in case writing errors to output was requested
            // Other errors are bubbled up to the caller
            catch (RuntimeException e)
            {
                if (e is PSRemotingTransportException)
                {
                    _ = System.Threading.Tasks.Task.Run(
                        () => _psesHost.UnwindCallStack(),
                        CancellationToken.None)
                        .HandleErrorsAsync(_logger);

                    _psesHost.WaitForExternalDebuggerStops();
                    throw new OperationCanceledException("The operation was canceled.", e);
                }

                Logger.LogWarning($"Runtime exception occurred while executing command:{Environment.NewLine}{Environment.NewLine}{e}");

                if (PowerShellExecutionOptions.ThrowOnError)
                {
                    throw;
                }

                PSCommand command = new PSCommand()
                    .AddOutputCommand()
                    .AddParameter("InputObject", e.ErrorRecord.AsPSObject());

                if (_pwsh.Runspace.RunspaceStateInfo.IsUsable())
                {
                    _pwsh.InvokeCommand(command);
                }
                else
                {
                    _psesHost.UI.WriteErrorLine(e.ToString());
                }
            }
            finally
            {
                if (_pwsh.HadErrors)
                {
                    _pwsh.Streams.Error.Clear();
                }
            }

            return result;
        }

        private IReadOnlyList<TResult> ExecuteInDebugger(CancellationToken cancellationToken)
        {
            // TODO: How much of this method can we remove now that it only processes PowerShell's
            // intrinsic debugger commands?
            cancellationToken.Register(CancelDebugExecution);

            PSDataCollection<PSObject> outputCollection = new();

            // Out-Default doesn't work as needed in the debugger
            // Instead we add Out-String to the command and collect results in a PSDataCollection
            // and use the event handler to print output to the UI as its added to that collection
            if (PowerShellExecutionOptions.WriteOutputToHost)
            {
                _psCommand.AddDebugOutputCommand();

                // Use an inline delegate here, since otherwise we need a cast -- allocation < cast
                outputCollection.DataAdded += (object sender, DataAddedEventArgs args) =>
                    {
                        for (int i = args.Index; i < outputCollection.Count; i++)
                        {
                            _psesHost.UI.WriteLine(outputCollection[i].ToString());
                        }
                    };
            }

            DebuggerCommandResults debuggerResult = null;
            try
            {
                // In the PowerShell debugger, intrinsic debugger commands are made available, like
                // "l", "s", "c", etc. Executing those commands produces a result that needs to be
                // set on the debugger stop event args. So we use the Debugger.ProcessCommand() API
                // to properly execute commands in the debugger and then call
                // DebugContext.ProcessDebuggerResult() later to handle the command appropriately
                //
                // Unfortunately, this API does not allow us to pass in the InvocationSettings,
                // which means (for instance) that we cannot instruct it to avoid adding our
                // debugger implmentation's commands to the history. So instead we now only call
                // `ExecuteInDebugger` for PowerShell's own intrinsic debugger commands.
                debuggerResult = _pwsh.Runspace.Debugger.ProcessCommand(_psCommand, outputCollection);
                cancellationToken.ThrowIfCancellationRequested();
            }
            // Allow terminate exceptions to propogate for flow control.
            catch (TerminateException)
            {
                throw;
            }
            // Test if we've been cancelled. If we're remoting, PSRemotingDataStructureException
            // effectively means the pipeline was stopped.
            catch (Exception e) when (cancellationToken.IsCancellationRequested || e is PipelineStoppedException || e is PSRemotingDataStructureException)
            {
                StopDebuggerIfRemoteDebugSessionFailed();
                throw new OperationCanceledException();
            }
            // We only catch RuntimeExceptions here in case writing errors to output was requested
            // Other errors are bubbled up to the caller
            catch (RuntimeException e)
            {
                if (e is PSRemotingTransportException)
                {
                    _ = System.Threading.Tasks.Task.Run(
                        () => _psesHost.UnwindCallStack(),
                        CancellationToken.None)
                        .HandleErrorsAsync(_logger);

                    _psesHost.WaitForExternalDebuggerStops();
                    throw new OperationCanceledException("The operation was canceled.", e);
                }

                Logger.LogWarning($"Runtime exception occurred while executing command:{Environment.NewLine}{Environment.NewLine}{e}");

                if (PowerShellExecutionOptions.ThrowOnError)
                {
                    throw;
                }

                using PSDataCollection<PSObject> errorOutputCollection = new();
                errorOutputCollection.DataAdding += (object sender, DataAddingEventArgs args)
                    => _psesHost.UI.WriteLine(args.ItemAdded?.ToString());

                PSCommand command = new PSCommand()
                    .AddDebugOutputCommand()
                    .AddParameter("InputObject", e.ErrorRecord.AsPSObject());

                _pwsh.Runspace.Debugger.ProcessCommand(command, errorOutputCollection);
            }
            finally
            {
                if (_pwsh.HadErrors)
                {
                    _pwsh.Streams.Error.Clear();
                }
            }

            _psesHost.DebugContext.ProcessDebuggerResult(debuggerResult);

            // Optimisation to save wasted computation if we're going to throw the output away anyway
            if (PowerShellExecutionOptions.WriteOutputToHost)
            {
                return Array.Empty<TResult>();
            }

            // If we've been asked for a PSObject, no need to allocate a new collection
            if (typeof(TResult) == typeof(PSObject)
                && outputCollection is IReadOnlyList<TResult> resultCollection)
            {
                return resultCollection;
            }

            // Otherwise, convert things over
            List<TResult> results = new(outputCollection.Count);
            foreach (PSObject outputResult in outputCollection)
            {
                if (LanguagePrimitives.TryConvertTo(outputResult, typeof(TResult), out object result))
                {
                    results.Add((TResult)result);
                }
            }
            return results;
        }

        private void StopDebuggerIfRemoteDebugSessionFailed()
        {
            // When remoting to Windows PowerShell,
            // command cancellation may cancel the remote debug session in a way that the local debug session doesn't detect.
            // Instead we have to query the remote directly
            if (_pwsh.Runspace.RunspaceIsRemote)
            {
                _pwsh.Runspace.ThrowCancelledIfUnusable();
                PSCommand assessDebuggerCommand = new PSCommand().AddScript("$Host.Runspace.Debugger.InBreakpoint");

                PSDataCollection<PSObject> outputCollection = new();
                _pwsh.Runspace.Debugger.ProcessCommand(assessDebuggerCommand, outputCollection);

                foreach (PSObject output in outputCollection)
                {
                    if (Equals(output?.BaseObject, false))
                    {
                        _psesHost.DebugContext.ProcessDebuggerResult(new DebuggerCommandResults(DebuggerResumeAction.Stop, evaluatedByDebugger: true));
                        _logger.LogWarning("Cancelling debug session due to remote command cancellation causing the end of remote debugging session");
                        _psesHost.UI.WriteWarningLine("Debug session aborted by command cancellation. This is a known issue in the Windows PowerShell 5.1 remoting system.");
                    }
                }
            }
        }

        private void CancelNormalExecution()
        {
            if (!_pwsh.Runspace.RunspaceStateInfo.IsUsable())
            {
                return;
            }

            // If we're signaled to exit a runspace then that'll trigger a stop,
            // if we block on that stop we'll never exit the runspace (
            // and essentially deadlock).
            if (_frame?.SessionExiting is true)
            {
                _pwsh.BeginStop(null, null);
                return;
            }

            try
            {
                _pwsh.Stop();
            }
            catch (NullReferenceException nre)
            {
                _logger.LogError(
                    nre,
                    "Null reference exception from PowerShell.Stop received.");
            }
        }

        private void CancelDebugExecution()
        {
            if (_pwsh.Runspace.RunspaceStateInfo.IsUsable())
            {
                return;
            }

            _pwsh.Runspace.Debugger.StopProcessCommand();
        }
    }
}
