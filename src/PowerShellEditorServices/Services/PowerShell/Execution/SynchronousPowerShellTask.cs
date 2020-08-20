using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Remoting;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal class SynchronousPowerShellTask<TResult> : SynchronousTask<IReadOnlyList<TResult>>
    {
        private readonly ILogger _logger;

        private readonly PowerShellContext _pwshContext;

        private readonly PSCommand _psCommand;

        private readonly PowerShellExecutionOptions _executionOptions;

        private SMA.PowerShell _pwsh;

        private PSHost _psHost;

        public SynchronousPowerShellTask(
            ILogger logger,
            PowerShellContext pwshContext,
            PSCommand command,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _logger = logger;
            _pwshContext = pwshContext;
            _psCommand = command;
            _executionOptions = executionOptions;
        }

        public override IReadOnlyList<TResult> Run(CancellationToken cancellationToken)
        {
            _pwsh = _pwshContext.CurrentPowerShell;
            _psHost = _pwshContext.EditorServicesPSHost;

            if (_executionOptions.WriteInputToHost)
            {
                _psHost.UI.WriteLine(_psCommand.GetInvocationText());
            }

            return !_executionOptions.NoDebuggerExecution && _pwsh.Runspace.Debugger.InBreakpoint
                ? ExecuteInDebugger(cancellationToken)
                : ExecuteNormally(cancellationToken);
        }

        public override string ToString()
        {
            return _psCommand.GetInvocationText();
        }

        private IReadOnlyList<TResult> ExecuteNormally(CancellationToken cancellationToken)
        {
            if (_executionOptions.WriteOutputToHost)
            {
                _psCommand.AddOutputCommand();
            }

            cancellationToken.Register(CancelNormalExecution);

            Collection<TResult> result = null;
            try
            {
                result = _pwsh.InvokeCommand<TResult>(_psCommand);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception e) when (cancellationToken.IsCancellationRequested || e is PipelineStoppedException || e is PSRemotingDataStructureException)
            {
                throw new OperationCanceledException();
            }
            catch (RuntimeException e)
            {
                Logger.LogWarning($"Runtime exception occurred while executing command:{Environment.NewLine}{Environment.NewLine}{e}");

                if (!_executionOptions.WriteOutputToHost)
                {
                    throw;
                }

                var command = new PSCommand()
                    .AddOutputCommand()
                    .AddParameter("InputObject", e.ErrorRecord.AsPSObject());

                _pwsh.InvokeCommand(command);
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
            cancellationToken.Register(CancelDebugExecution);

            var outputCollection = new PSDataCollection<PSObject>();

            if (_executionOptions.WriteOutputToHost)
            {
                _psCommand.AddDebugOutputCommand();

                // Use an inline delegate here, since otherwise we need a cast -- allocation < cast
                outputCollection.DataAdded += (object sender, DataAddedEventArgs args) =>
                    {
                        for (int i = args.Index; i < outputCollection.Count; i++)
                        {
                            _psHost.UI.WriteLine(outputCollection[i].ToString());
                        }
                    };
            }

            DebuggerCommandResults debuggerResult = null;
            try
            {
                debuggerResult = _pwsh.Runspace.Debugger.ProcessCommand(_psCommand, outputCollection);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception e) when (cancellationToken.IsCancellationRequested || e is PipelineStoppedException || e is PSRemotingDataStructureException)
            {
                StopDebuggerIfRemoteDebugSessionFailed();
                throw new OperationCanceledException();
            }
            catch (RuntimeException e)
            {
                Logger.LogWarning($"Runtime exception occurred while executing command:{Environment.NewLine}{Environment.NewLine}{e}");

                if (!_executionOptions.WriteOutputToHost)
                {
                    throw;
                }

                var errorOutputCollection = new PSDataCollection<PSObject>();
                errorOutputCollection.DataAdded += (object sender, DataAddedEventArgs args) =>
                    {
                        for (int i = args.Index; i < outputCollection.Count; i++)
                        {
                            _psHost.UI.WriteLine(outputCollection[i].ToString());
                        }
                    };

                var command = new PSCommand()
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

            _pwshContext.ProcessDebuggerResult(debuggerResult);

            // Optimisation to save wasted computation if we're going to throw the output away anyway
            if (_executionOptions.WriteOutputToHost)
            {
                return Array.Empty<TResult>();
            }

            // If we've been asked for a PSObject, no need to allocate a new collection
            if (typeof(TResult) == typeof(PSObject))
            {
                return (IReadOnlyList<TResult>)outputCollection;
            }

            // Otherwise, convert things over
            var results = new List<TResult>(outputCollection.Count);
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
                var assessDebuggerCommand = new PSCommand().AddScript("$Host.Runspace.Debugger.InBreakpoint");

                var outputCollection = new PSDataCollection<PSObject>();
                _pwsh.Runspace.Debugger.ProcessCommand(assessDebuggerCommand, outputCollection);

                foreach (PSObject output in outputCollection)
                {
                    if (object.Equals(output?.BaseObject, false))
                    {
                        _pwshContext.ProcessDebuggerResult(new DebuggerCommandResults(DebuggerResumeAction.Stop, evaluatedByDebugger: true));
                        _logger.LogWarning("Cancelling debug session due to remote command cancellation causing the end of remote debugging session");
                        _psHost.UI.WriteWarningLine("Debug session aborted by command cancellation. This is a known issue in the Windows PowerShell 5.1 remoting system.");
                    }
                }
            }
        }

        private void CancelNormalExecution()
        {
            _pwsh.Stop();
        }

        private void CancelDebugExecution()
        {
            _pwsh.Runspace.Debugger.StopProcessCommand();
        }
    }
}
