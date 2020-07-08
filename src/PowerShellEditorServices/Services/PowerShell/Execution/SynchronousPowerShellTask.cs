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

            cancellationToken.Register(Cancel);

            if (_executionOptions.WriteInputToHost)
            {
                _psHost.UI.WriteLine(_psCommand.GetInvocationText());
            }

            if (_pwsh.Runspace.Debugger.IsActive)
            {
                return ExecuteInDebugger(cancellationToken);
            }

            return ExecuteNormally(cancellationToken);
        }

        public override string ToString()
        {
            return _psCommand.GetInvocationText();
        }

        private IReadOnlyList<TResult> ExecuteNormally(CancellationToken cancellationToken)
        {
            _pwsh.Commands = _psCommand;

            if (_executionOptions.WriteOutputToHost)
            {
                _pwsh.AddOutputCommand();
            }

            Collection<TResult> result = null;
            try
            {
                result = _pwsh.InvokeAndClear<TResult>();

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

                _pwsh.AddOutputCommand()
                    .AddParameter("InputObject", e.ErrorRecord.AsPSObject())
                    .InvokeAndClear();
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
            var outputCollection = new PSDataCollection<PSObject>();
            DebuggerCommandResults debuggerResult = _pwsh.Runspace.Debugger.ProcessCommand(_psCommand, outputCollection);
            _pwshContext.ProcessDebuggerResult(debuggerResult);

            if (typeof(TResult) == typeof(PSObject))
            {
                return (IReadOnlyList<TResult>)outputCollection;
            }

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

        private void Cancel()
        {
            _pwsh.Stop();
        }
    }
}
