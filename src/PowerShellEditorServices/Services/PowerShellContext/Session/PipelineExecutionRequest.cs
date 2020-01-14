//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal interface IPipelineExecutionRequest
    {
        Task ExecuteAsync();

        Task WaitTask { get; }
    }

    /// <summary>
    /// Contains details relating to a request to execute a
    /// command on the PowerShell pipeline thread.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the execution.</typeparam>
    internal class PipelineExecutionRequest<TResult> : IPipelineExecutionRequest
    {
        private PowerShellContextService _powerShellContext;
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
            PowerShellContextService powerShellContext,
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
            PowerShellContextService powerShellContext,
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

        public async Task ExecuteAsync()
        {
            var results =
                await _powerShellContext.ExecuteCommandAsync<TResult>(
                    _psCommand,
                    _errorMessages,
                    _executionOptions).ConfigureAwait(false);

            _ = Task.Run(() => _resultsTask.SetResult(results));
        }
    }
}
