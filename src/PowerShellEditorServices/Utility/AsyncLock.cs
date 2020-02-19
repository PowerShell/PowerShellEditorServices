//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides a simple wrapper over a SemaphoreSlim to allow
    /// synchronization locking inside of async calls.  Cannot be
    /// used recursively.
    /// </summary>
    internal class AsyncLock
    {
        #region Fields

        private Task<IDisposable> lockReleaseTask;
        private SemaphoreSlim lockSemaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the AsyncLock class.
        /// </summary>
        public AsyncLock()
        {
            this.lockReleaseTask =
                Task.FromResult(
                    (IDisposable)new LockReleaser(this));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Locks
        /// </summary>
        /// <returns>A task which has an IDisposable</returns>
        public Task<IDisposable> LockAsync()
        {
            return this.LockAsync(CancellationToken.None);
        }

        /// <summary>
        /// Obtains or waits for a lock which can be used to synchronize
        /// access to a resource.  The wait may be cancelled with the
        /// given CancellationToken.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken which can be used to cancel the lock.
        /// </param>
        /// <returns></returns>
        public Task<IDisposable> LockAsync(CancellationToken cancellationToken)
        {
            Task waitTask = lockSemaphore.WaitAsync(cancellationToken);

            return waitTask.IsCompleted ?
                this.lockReleaseTask :
                waitTask.ContinueWith(
                    (t, releaser) =>
                    {
                        return (IDisposable)releaser;
                    },
                    this.lockReleaseTask.Result,
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        /// <summary>
        /// Obtains or waits for a lock which can be used to synchronize
        /// access to a resource.
        /// </summary>
        /// <returns></returns>
        public IDisposable Lock()
        {
            return Lock(CancellationToken.None);
        }

        /// <summary>
        /// Obtains or waits for a lock which can be used to synchronize
        /// access to a resource.  The wait may be cancelled with the
        /// given CancellationToken.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken which can be used to cancel the lock.
        /// </param>
        /// <returns></returns>
        public IDisposable Lock(CancellationToken cancellationToken)
        {
            lockSemaphore.Wait(cancellationToken);
            return this.lockReleaseTask.Result;
        }

        #endregion

        #region Private Classes

        /// <summary>
        /// Provides an IDisposable wrapper around an AsyncLock so
        /// that it can easily be used inside of a 'using' block.
        /// </summary>
        private class LockReleaser : IDisposable
        {
            private AsyncLock lockToRelease;

            internal LockReleaser(AsyncLock lockToRelease)
            {
                this.lockToRelease = lockToRelease;
            }

            public void Dispose()
            {
                this.lockToRelease.lockSemaphore.Release();
            }
        }

        #endregion
    }
}
