using Microsoft.Extensions.Logging;
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

        private readonly PSHost _psHost;

        private readonly PowerShellExecutionOptions _executionOptions;

        private SMA.PowerShell _pwsh;

        public SynchronousPowerShellTask(
            ILogger logger,
            PowerShellContext pwshContext,
            PSHost psHost,
            PSCommand command,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _logger = logger;
            _pwshContext = pwshContext;
            _psHost = psHost;
            _psCommand = command;
            _executionOptions = executionOptions;
        }

        public override IReadOnlyList<TResult> Run(CancellationToken cancellationToken)
        {
            _pwsh = _pwshContext.CurrentPowerShell;

            if (_executionOptions.WriteInputToHost)
            {
                _psHost.UI.WriteLine(_psCommand.GetInvocationText());
            }

            return _pwsh.Runspace.Debugger.IsActive
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

                if (_executionOptions.PropagateCancellationToCaller)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (Exception e) when (e is PipelineStoppedException || e is PSRemotingDataStructureException)
            {
                string message = $"Pipeline stopped while executing command:{Environment.NewLine}{Environment.NewLine}{e}";
                Logger.LogError(message);
                throw new ExecutionCanceledException(message, e);
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

            DebuggerCommandResults debuggerResult = _pwsh.Runspace.Debugger.ProcessCommand(_psCommand, outputCollection);
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
