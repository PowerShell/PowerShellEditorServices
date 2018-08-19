//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Session
{
    using System.Management.Automation;

    /// <summary>
    /// Provides the ability to take over the current pipeline in a runspace.
    /// </summary>
    internal class InvocationEventQueue : IDisposable
    {
        private const string ShouldProcessInExecutionThreadPropertyName = "ShouldProcessInExecutionThread";

        private static readonly PropertyInfo s_shouldProcessInExecutionThreadProperty =
            typeof(PSEventSubscriber)
                .GetProperty(
                    ShouldProcessInExecutionThreadPropertyName,
                    BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PromptNest _promptNest;

        private readonly Runspace _runspace;

        private readonly PowerShellContext _powerShellContext;

        private readonly ILogger _logger;

        private bool _isDisposed;

        private InvocationRequest _invocationRequest;

        private PSEventSubscriber _onIdleSubscriber;

        private SemaphoreSlim _pipelineRequestHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private SemaphoreSlim _subscriberHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private InvocationEventQueue(
            PowerShellContext powerShellContext,
            PromptNest promptNest,
            ILogger logger)
        {
            _promptNest = promptNest;
            _powerShellContext = powerShellContext;
            _runspace = powerShellContext.CurrentRunspace.Runspace;
            _logger = logger;
        }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                RemoveInvocationSubscriber();
            }

            _isDisposed = true;
        }

        internal static InvocationEventQueue Create(
            PowerShellContext powerShellContext,
            PromptNest promptNest,
            ILogger logger)
        {
            var eventQueue = new InvocationEventQueue(powerShellContext, promptNest, logger);
            eventQueue.CreateInvocationSubscriber();
            return eventQueue;
        }

        /// <summary>
        /// Creates a <see cref="PSEventSubscriber" /> subscribed the engine event
        /// <see cref="PSEngineEvent.OnIdle" /> that handles requests for pipeline thread access.
        /// </summary>
        /// <returns>
        /// The newly created <see cref="PSEventSubscriber" /> or an existing subscriber if
        /// creation already occurred.
        /// </returns>
        internal PSEventSubscriber CreateInvocationSubscriber()
        {
            _subscriberHandle.Wait();
            try
            {
                if (_onIdleSubscriber != null)
                {
                    _logger.Write(
                        LogLevel.Error,
                        "An attempt to create the ReadLine OnIdle subscriber was made when one already exists.");
                    return _onIdleSubscriber;
                }

                _onIdleSubscriber = _runspace.Events.SubscribeEvent(
                    source: null,
                    eventName: PSEngineEvent.OnIdle,
                    sourceIdentifier: PSEngineEvent.OnIdle,
                    data: null,
                    handlerDelegate: OnPowerShellIdle,
                    supportEvent: true,
                    forwardEvent: false);

                SetSubscriberExecutionThreadWithReflection(_onIdleSubscriber);

                _onIdleSubscriber.Unsubscribed += OnInvokerUnsubscribed;

                return _onIdleSubscriber;
            }
            finally
            {
                _subscriberHandle.Release();
            }
        }

        /// <summary>
        /// Unsubscribes the existing <see cref="PSEventSubscriber" /> handling pipeline thread
        /// access requests.
        /// </summary>
        internal void RemoveInvocationSubscriber()
        {
            _subscriberHandle.Wait();
            try
            {
                if (_onIdleSubscriber == null)
                {
                    _logger.Write(
                        LogLevel.Error,
                        "An attempt to remove the ReadLine OnIdle subscriber was made before it was created.");
                    return;
                }

                _onIdleSubscriber.Unsubscribed -= OnInvokerUnsubscribed;
                _runspace.Events.UnsubscribeEvent(_onIdleSubscriber);
                _onIdleSubscriber = null;
            }
            finally
            {
                _subscriberHandle.Release();
            }
        }

        /// <summary>
        /// Executes a command on the main pipeline thread through
        /// eventing. A <see cref="PSEngineEvent.OnIdle" /> event subscriber will
        /// be created that creates a nested PowerShell instance for
        /// <see cref="PowerShellContext.ExecuteCommand" /> to utilize.
        /// </summary>
        /// <remarks>
        /// Avoid using this method directly if possible.
        /// <see cref="PowerShellContext.ExecuteCommand" /> will route commands
        /// through this method if required.
        /// </remarks>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <param name="psCommand">The <see cref="PSCommand" /> to be executed.</param>
        /// <param name="errorMessages">
        /// Error messages from PowerShell will be written to the <see cref="StringBuilder" />.
        /// </param>
        /// <param name="executionOptions">Specifies options to be used when executing this command.</param>
        /// <returns>
        /// An awaitable <see cref="Task" /> which will provide results once the command
        /// execution completes.
        /// </returns>
        internal async Task<IEnumerable<TResult>> ExecuteCommandOnIdle<TResult>(
            PSCommand psCommand,
            StringBuilder errorMessages,
            ExecutionOptions executionOptions)
        {
            var request = new PipelineExecutionRequest<TResult>(
                _powerShellContext,
                psCommand,
                errorMessages,
                executionOptions);

            await SetInvocationRequestAsync(
                new InvocationRequest(
                    pwsh => request.Execute().GetAwaiter().GetResult()));

            try
            {
                return await request.Results;
            }
            finally
            {
                await SetInvocationRequestAsync(request: null);
            }
        }

        /// <summary>
        /// Marshals a <see cref="Action{PowerShell}" /> to run on the pipeline thread. A new
        /// <see cref="PromptNestFrame" /> will be created for the invocation.
        /// </summary>
        /// <param name="invocationAction">
        /// The <see cref="Action{PowerShell}" /> to invoke on the pipeline thread. The nested
        /// <see cref="PowerShell" /> instance for the created <see cref="PromptNestFrame" />
        /// will be passed as an argument.
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task" /> that the caller can use to know when execution completes.
        /// </returns>
        internal async Task InvokeOnPipelineThread(Action<PowerShell> invocationAction)
        {
            var request = new InvocationRequest(pwsh =>
            {
                using (_promptNest.GetRunspaceHandle(CancellationToken.None, isReadLine: false))
                {
                    pwsh.Runspace = _runspace;
                    invocationAction(pwsh);
                }
            });

            await SetInvocationRequestAsync(request);
            try
            {
                await request.Task;
            }
            finally
            {
                await SetInvocationRequestAsync(null);
            }
        }

        private async Task WaitForExistingRequestAsync()
        {
            InvocationRequest existingRequest;
            await _pipelineRequestHandle.WaitAsync();
            try
            {
                existingRequest = _invocationRequest;
                if (existingRequest == null || existingRequest.Task.IsCompleted)
                {
                    return;
                }
            }
            finally
            {
                _pipelineRequestHandle.Release();
            }

            await existingRequest.Task;
        }

        private async Task SetInvocationRequestAsync(InvocationRequest request)
        {
            await WaitForExistingRequestAsync();
            await _pipelineRequestHandle.WaitAsync();
            try
            {
                _invocationRequest = request;
            }
            finally
            {
                _pipelineRequestHandle.Release();
            }

            _powerShellContext.ForcePSEventHandling();
        }

        private void OnPowerShellIdle(object sender, EventArgs e)
        {
            if (!_pipelineRequestHandle.Wait(0))
            {
                return;
            }

            InvocationRequest currentRequest = null;
            try
            {
                if (_invocationRequest == null)
                {
                    return;
                }

                 currentRequest = _invocationRequest;
            }
            finally
            {
                _pipelineRequestHandle.Release();
            }

            _promptNest.PushPromptContext();
            try
            {
                currentRequest.Invoke(_promptNest.GetPowerShell());
            }
            finally
            {
                _promptNest.PopPromptContext();
            }
        }

        private void OnInvokerUnsubscribed(object sender, PSEventUnsubscribedEventArgs e)
        {
            CreateInvocationSubscriber();
        }

        private void SetSubscriberExecutionThreadWithReflection(PSEventSubscriber subscriber)
        {
            // We need to create the PowerShell object in the same thread so we can get a nested
            // PowerShell.  This is the only way to consistently take control of the pipeline.  The
            // alternative is to make the subscriber a script block and have that create and process
            // the PowerShell object, but that puts us in a different SessionState and is a lot slower.
            s_shouldProcessInExecutionThreadProperty.SetValue(subscriber, true);
        }

        private class InvocationRequest : TaskCompletionSource<bool>
        {
            private readonly Action<PowerShell> _invocationAction;

            internal InvocationRequest(Action<PowerShell> invocationAction)
            {
                _invocationAction = invocationAction;
            }

            internal void Invoke(PowerShell pwsh)
            {
                try
                {
                    _invocationAction(pwsh);

                    // Ensure the result is set in another thread otherwise the caller
                    // may take over the pipeline thread.
                    System.Threading.Tasks.Task.Run(() => SetResult(true));
                }
                catch (Exception e)
                {
                    System.Threading.Tasks.Task.Run(() => SetException(e));
                }
            }
        }
    }
}
