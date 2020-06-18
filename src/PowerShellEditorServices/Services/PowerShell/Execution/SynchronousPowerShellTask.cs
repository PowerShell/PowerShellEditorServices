using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal class SynchronousPowerShellTask<TResult> : SynchronousTask<Collection<TResult>>
    {
        private readonly SMA.PowerShell _pwsh;

        private readonly PSCommand _psCommand;

        private readonly PowerShellExecutionOptions _executionOptions;

        public SynchronousPowerShellTask(
            ILogger logger,
            SMA.PowerShell pwsh,
            PSCommand command,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _pwsh = pwsh;
            _psCommand = command;
            _executionOptions = executionOptions;
        }

        public override Collection<TResult> Run(CancellationToken cancellationToken)
        {
            cancellationToken.Register(Cancel);

            _pwsh.Commands = _psCommand;

            if (_executionOptions.WriteOutputToHost)
            {
                _pwsh.AddOutputCommand();
            }

            Collection<TResult> result = null;
            try
            {
                result = _pwsh.InvokeAndClear<TResult>();
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

                if (_executionOptions.WriteErrorsToHost)
                {
                    _pwsh.AddOutputCommand()
                        .AddParameter("InputObject", e.ErrorRecord.AsPSObject())
                        .InvokeAndClear();
                }

                throw;
            }


            if (_pwsh.HadErrors)
            {
                _pwsh.Streams.Error.Clear();
            }

            return result;
        }

        private void Cancel()
        {
            _pwsh.Stop();
        }
    }
}
