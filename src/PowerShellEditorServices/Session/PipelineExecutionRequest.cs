using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Session
{
    internal interface IPipelineExecutionRequest
    {
        Task Execute();

        Task WaitTask { get; }
    }

    /// <summary>
    /// Contains details relating to a request to execute a
    /// command on the PowerShell pipeline thread.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the execution.</typeparam>
    internal class PipelineExecutionRequest<TResult> : IPipelineExecutionRequest
    {
        private PowerShellContext _powerShellContext;
        private PSCommand _psCommand;
        private StringBuilder _errorMessages;
        private ExecutionOptions _executionOptions;
        private TaskCompletionSource<IEnumerable<TResult>> _resultsTask;

        public Task<IEnumerable<TResult>> Results
        {
            get { return this._resultsTask.Task; }
        }

        public Task WaitTask { get { return Results; } }

        public PipelineExecutionRequest(
            PowerShellContext powerShellContext,
            PSCommand psCommand,
            StringBuilder errorMessages,
            bool sendOutputToHost)
            : this(
                powerShellContext,
                psCommand,
                errorMessages,
                new ExecutionOptions()
                {
                    WriteOutputToHost = sendOutputToHost
                })
            { }


        public PipelineExecutionRequest(
            PowerShellContext powerShellContext,
            PSCommand psCommand,
            StringBuilder errorMessages,
            ExecutionOptions executionOptions)
        {
            _powerShellContext = powerShellContext;
            _psCommand = psCommand;
            _errorMessages = errorMessages;
            _executionOptions = executionOptions;
            _resultsTask = new TaskCompletionSource<IEnumerable<TResult>>();
        }

        public async Task Execute()
        {
            var results =
                await _powerShellContext.ExecuteCommand<TResult>(
                    _psCommand,
                    _errorMessages,
                    _executionOptions);

            var unusedTask = Task.Run(() => _resultsTask.SetResult(results));
            // TODO: Deal with errors?
        }
    }
}
