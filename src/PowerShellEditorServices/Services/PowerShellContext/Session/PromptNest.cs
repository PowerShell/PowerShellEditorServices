//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    using System;
    using System.Management.Automation;

    /// <summary>
    /// Represents the stack of contexts in which PowerShell commands can be invoked.
    /// </summary>
    internal class PromptNest : IDisposable
    {
        private readonly ConcurrentStack<PromptNestFrame> _frameStack;

        private readonly PromptNestFrame _readLineFrame;

        private readonly IVersionSpecificOperations _versionSpecificOperations;

        private readonly object _syncObject = new object();

        private readonly object _disposeSyncObject = new object();

        private IHostInput _consoleReader;

        private PowerShellContextService _powerShellContext;

        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromptNest" /> class.
        /// </summary>
        /// <param name="powerShellContext">
        /// The <see cref="PowerShellContext" /> to track prompt status for.
        /// </param>
        /// <param name="initialPowerShell">
        /// The <see cref="PowerShell" /> instance for the first frame.
        /// </param>
        /// <param name="consoleReader">
        /// The input handler.
        /// </param>
        /// <param name="versionSpecificOperations">
        /// The <see cref="IVersionSpecificOperations" /> for the calling
        /// <see cref="PowerShellContext" /> instance.
        /// </param>
        /// <remarks>
        /// This constructor should only be called when <see cref="PowerShellContext.CurrentRunspace" />
        /// is set to the initial runspace.
        /// </remarks>
        internal PromptNest(
            PowerShellContextService powerShellContext,
            PowerShell initialPowerShell,
            IHostInput consoleReader,
            IVersionSpecificOperations versionSpecificOperations)
        {
            _versionSpecificOperations = versionSpecificOperations;
            _consoleReader = consoleReader;
            _powerShellContext = powerShellContext;
            _frameStack = new ConcurrentStack<PromptNestFrame>();
            _frameStack.Push(
                new PromptNestFrame(
                    initialPowerShell,
                    NewHandleQueue()));

            var readLineShell = PowerShell.Create();
            readLineShell.Runspace = powerShellContext.CurrentRunspace.Runspace;
            _readLineFrame = new PromptNestFrame(
                readLineShell,
                new AsyncQueue<RunspaceHandle>());

            ReleaseRunspaceHandleImpl(isReadLine: true);
        }

        /// <summary>
        /// Gets a value indicating whether the current frame was created by a debugger stop event.
        /// </summary>
        internal bool IsInDebugger => CurrentFrame.FrameType.HasFlag(PromptNestFrameType.Debug);

        /// <summary>
        /// Gets a value indicating whether the current frame was created for an out of process runspace.
        /// </summary>
        internal bool IsRemote => CurrentFrame.FrameType.HasFlag(PromptNestFrameType.Remote);

        /// <summary>
        /// Gets a value indicating whether the current frame was created by PSHost.EnterNestedPrompt().
        /// </summary>
        internal bool IsNestedPrompt => CurrentFrame.FrameType.HasFlag(PromptNestFrameType.NestedPrompt);

        /// <summary>
        /// Gets a value indicating the current number of frames managed by this PromptNest.
        /// </summary>
        internal int NestedPromptLevel => _frameStack.Count;

        private PromptNestFrame CurrentFrame
        {
            get
            {
                _frameStack.TryPeek(out PromptNestFrame currentFrame);
                return _isDisposed ? _readLineFrame : currentFrame;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_disposeSyncObject)
            {
                if (_isDisposed || !disposing)
                {
                    return;
                }

                while (NestedPromptLevel > 1)
                {
                    _consoleReader?.StopCommandLoop();
                    var currentFrame = CurrentFrame;
                    if (currentFrame.FrameType.HasFlag(PromptNestFrameType.Debug))
                    {
                        _versionSpecificOperations.StopCommandInDebugger(_powerShellContext);
                        currentFrame.ThreadController.StartThreadExit(DebuggerResumeAction.Stop);
                        currentFrame.WaitForFrameExit(CancellationToken.None);
                        continue;
                    }

                    if (currentFrame.FrameType.HasFlag(PromptNestFrameType.NestedPrompt))
                    {
                        _powerShellContext.ExitAllNestedPrompts();
                        continue;
                    }

                    currentFrame.PowerShell.BeginStop(null, null);
                    currentFrame.WaitForFrameExit(CancellationToken.None);
                }

                _consoleReader?.StopCommandLoop();
                _readLineFrame.Dispose();
                CurrentFrame.Dispose();
                _frameStack.Clear();
                _powerShellContext = null;
                _consoleReader = null;
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Gets the <see cref="ThreadController" /> for the current frame.
        /// </summary>
        /// <returns>
        /// The <see cref="ThreadController" /> for the current frame, or
        /// <see langword="null" /> if the current frame does not have one.
        /// </returns>
        internal ThreadController GetThreadController()
        {
            if (_isDisposed)
            {
                return null;
            }

            return CurrentFrame.IsThreadController ? CurrentFrame.ThreadController : null;
        }

        /// <summary>
        /// Create a new <see cref="PromptNestFrame" /> and set it as the current frame.
        /// </summary>
        internal void PushPromptContext()
        {
            if (_isDisposed)
            {
                return;
            }

            PushPromptContext(PromptNestFrameType.Normal);
        }

        /// <summary>
        /// Create a new <see cref="PromptNestFrame" /> and set it as the current frame.
        /// </summary>
        /// <param name="frameType">The frame type.</param>
        internal void PushPromptContext(PromptNestFrameType frameType)
        {
            if (_isDisposed)
            {
                return;
            }

            _frameStack.Push(
                new PromptNestFrame(
                    frameType.HasFlag(PromptNestFrameType.Remote)
                        ? PowerShell.Create()
                        : PowerShell.Create(RunspaceMode.CurrentRunspace),
                    NewHandleQueue(),
                    frameType));
        }

        /// <summary>
        /// Dispose of the current <see cref="PromptNestFrame" /> and revert to the previous frame.
        /// </summary>
        internal void PopPromptContext()
        {
            PromptNestFrame currentFrame;
            lock (_syncObject)
            {
                if (_isDisposed || _frameStack.Count == 1)
                {
                    return;
                }

                _frameStack.TryPop(out currentFrame);
            }

            currentFrame.Dispose();
        }

        /// <summary>
        /// Get the <see cref="PowerShell" /> instance for the current
        /// <see cref="PromptNestFrame" />.
        /// </summary>
        /// <param name="isReadLine">Indicates whether this is for a PSReadLine command.</param>
        /// <returns>The <see cref="PowerShell" /> instance for the current frame.</returns>
        internal PowerShell GetPowerShell(bool isReadLine = false)
        {
            if (_isDisposed)
            {
                return null;
            }

            // Typically we want to run PSReadLine on the current nest frame.
            // The exception is when the current frame is remote, in which
            // case we need to run it in it's own frame because we can't take
            // over a remote pipeline through event invocation.
            if (NestedPromptLevel > 1 && !IsRemote)
            {
                return CurrentFrame.PowerShell;
            }

            return isReadLine ? _readLineFrame.PowerShell : CurrentFrame.PowerShell;
        }

        /// <summary>
        /// Get the <see cref="RunspaceHandle" /> for the current <see cref="PromptNestFrame" />.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> that can be used to cancel the request.
        /// </param>
        /// <param name="isReadLine">Indicates whether this is for a PSReadLine command.</param>
        /// <returns>The <see cref="RunspaceHandle" /> for the current frame.</returns>
        internal RunspaceHandle GetRunspaceHandle(CancellationToken cancellationToken, bool isReadLine)
        {
            if (_isDisposed)
            {
                return null;
            }

            // Also grab the main runspace handle if this is for a ReadLine pipeline and the runspace
            // is in process.
            if (isReadLine && !_powerShellContext.IsCurrentRunspaceOutOfProcess())
            {
                GetRunspaceHandleImpl(cancellationToken, isReadLine: false);
            }

            return GetRunspaceHandleImpl(cancellationToken, isReadLine);
        }


        /// <summary>
        /// Get the <see cref="RunspaceHandle" /> for the current <see cref="PromptNestFrame" />.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> that will be checked prior to
        /// completing the returned task.
        /// </param>
        /// <param name="isReadLine">Indicates whether this is for a PSReadLine command.</param>
        /// <returns>
        /// A <see cref="Task{RunspaceHandle}" /> object representing the asynchronous operation.
        /// The <see cref="Task{RunspaceHandle}.Result" /> property will return the
        /// <see cref="RunspaceHandle" /> for the current frame.
        /// </returns>
        internal async Task<RunspaceHandle> GetRunspaceHandleAsync(CancellationToken cancellationToken, bool isReadLine)
        {
            if (_isDisposed)
            {
                return null;
            }

            // Also grab the main runspace handle if this is for a ReadLine pipeline and the runspace
            // is in process.
            if (isReadLine && !_powerShellContext.IsCurrentRunspaceOutOfProcess())
            {
                await GetRunspaceHandleImplAsync(cancellationToken, isReadLine: false).ConfigureAwait(false);
            }

            return await GetRunspaceHandleImplAsync(cancellationToken, isReadLine).ConfigureAwait(false);
        }

        /// <summary>
        /// Releases control of the runspace aquired via the <see cref="RunspaceHandle" />.
        /// </summary>
        /// <param name="runspaceHandle">
        /// The <see cref="RunspaceHandle" /> representing the control to release.
        /// </param>
        internal void ReleaseRunspaceHandle(RunspaceHandle runspaceHandle)
        {
            if (_isDisposed)
            {
                return;
            }

            ReleaseRunspaceHandleImpl(runspaceHandle.IsReadLine);
            if (runspaceHandle.IsReadLine && !_powerShellContext.IsCurrentRunspaceOutOfProcess())
            {
                ReleaseRunspaceHandleImpl(isReadLine: false);
            }
        }

        /// <summary>
        /// Releases control of the runspace aquired via the <see cref="RunspaceHandle" />.
        /// </summary>
        /// <param name="runspaceHandle">
        /// The <see cref="RunspaceHandle" /> representing the control to release.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> object representing the release of the
        /// <see cref="RunspaceHandle" />.
        /// </returns>
        internal async Task ReleaseRunspaceHandleAsync(RunspaceHandle runspaceHandle)
        {
            if (_isDisposed)
            {
                return;
            }

            await ReleaseRunspaceHandleImplAsync(runspaceHandle.IsReadLine).ConfigureAwait(false);
            if (runspaceHandle.IsReadLine && !_powerShellContext.IsCurrentRunspaceOutOfProcess())
            {
                await ReleaseRunspaceHandleImplAsync(isReadLine: false).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Determines if the current frame is unavailable for commands.
        /// </summary>
        /// <returns>
        /// A value indicating whether the current frame is unavailable for commands.
        /// </returns>
        internal bool IsMainThreadBusy()
        {
            return !_isDisposed && CurrentFrame.Queue.IsEmpty;
        }

        /// <summary>
        /// Determines if a PSReadLine command is currently running.
        /// </summary>
        /// <returns>
        /// A value indicating whether a PSReadLine command is currently running.
        /// </returns>
        internal bool IsReadLineBusy()
        {
            return !_isDisposed && _readLineFrame.Queue.IsEmpty;
        }

        /// <summary>
        /// Blocks until the current frame has been disposed.
        /// </summary>
        /// <param name="initiator">
        /// A delegate that when invoked initates the exit of the current frame.
        /// </param>
        internal void WaitForCurrentFrameExit(Action<PromptNestFrame> initiator)
        {
            if (_isDisposed)
            {
                return;
            }

            var currentFrame = CurrentFrame;
            try
            {
                initiator.Invoke(currentFrame);
            }
            finally
            {
                currentFrame.WaitForFrameExit(CancellationToken.None);
            }
        }

        /// <summary>
        /// Blocks until the current frame has been disposed.
        /// </summary>
        internal void WaitForCurrentFrameExit()
        {
            if (_isDisposed)
            {
                return;
            }

            CurrentFrame.WaitForFrameExit(CancellationToken.None);
        }

        /// <summary>
        /// Blocks until the current frame has been disposed.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> used the exit the block prior to
        /// the current frame being disposed.
        /// </param>
        internal void WaitForCurrentFrameExit(CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return;
            }

            CurrentFrame.WaitForFrameExit(cancellationToken);
        }

        /// <summary>
        /// Creates a task that is completed when the current frame has been disposed.
        /// </summary>
        /// <param name="initiator">
        /// A delegate that when invoked initates the exit of the current frame.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> object representing the current frame being disposed.
        /// </returns>
        internal async Task WaitForCurrentFrameExitAsync(Func<PromptNestFrame, Task> initiator)
        {
            if (_isDisposed)
            {
                return;
            }

            var currentFrame = CurrentFrame;
            try
            {
                await initiator.Invoke(currentFrame).ConfigureAwait(false);
            }
            finally
            {
                await currentFrame.WaitForFrameExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a task that is completed when the current frame has been disposed.
        /// </summary>
        /// <param name="initiator">
        /// A delegate that when invoked initates the exit of the current frame.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> object representing the current frame being disposed.
        /// </returns>
        internal async Task WaitForCurrentFrameExitAsync(Action<PromptNestFrame> initiator)
        {
            if (_isDisposed)
            {
                return;
            }

            var currentFrame = CurrentFrame;
            try
            {
                initiator.Invoke(currentFrame);
            }
            finally
            {
                await currentFrame.WaitForFrameExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a task that is completed when the current frame has been disposed.
        /// </summary>
        /// <returns>
        /// A <see cref="Task" /> object representing the current frame being disposed.
        /// </returns>
        internal async Task WaitForCurrentFrameExitAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            await WaitForCurrentFrameExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a task that is completed when the current frame has been disposed.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> used the exit the block prior to the current frame being disposed.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> object representing the current frame being disposed.
        /// </returns>
        internal async Task WaitForCurrentFrameExitAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return;
            }

            await CurrentFrame.WaitForFrameExitAsync(cancellationToken).ConfigureAwait(false);
        }

        private AsyncQueue<RunspaceHandle> NewHandleQueue()
        {
            var queue = new AsyncQueue<RunspaceHandle>();
            queue.Enqueue(new RunspaceHandle(_powerShellContext));
            return queue;
        }

        private RunspaceHandle GetRunspaceHandleImpl(CancellationToken cancellationToken, bool isReadLine)
        {
            if (isReadLine)
            {
                return _readLineFrame.Queue.Dequeue(cancellationToken);
            }

            return CurrentFrame.Queue.Dequeue(cancellationToken);
        }

        private async Task<RunspaceHandle> GetRunspaceHandleImplAsync(CancellationToken cancellationToken, bool isReadLine)
        {
            if (isReadLine)
            {
                return await _readLineFrame.Queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
            }

            return await CurrentFrame.Queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        }

        private void ReleaseRunspaceHandleImpl(bool isReadLine)
        {
            if (isReadLine)
            {
                _readLineFrame.Queue.Enqueue(new RunspaceHandle(_powerShellContext, true));
                return;
            }

            CurrentFrame.Queue.Enqueue(new RunspaceHandle(_powerShellContext, false));
        }

        private async Task ReleaseRunspaceHandleImplAsync(bool isReadLine)
        {
            if (isReadLine)
            {
                await _readLineFrame.Queue.EnqueueAsync(new RunspaceHandle(_powerShellContext, true)).ConfigureAwait(false);
                return;
            }

            await CurrentFrame.Queue.EnqueueAsync(new RunspaceHandle(_powerShellContext, false)).ConfigureAwait(false);
        }
    }
}
