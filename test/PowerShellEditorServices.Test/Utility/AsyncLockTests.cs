//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class AsyncLockTests
    {
        [Fact]
        public async Task AsyncLockSynchronizesAccess()
        {
            AsyncLock asyncLock = new AsyncLock();

            Task<IDisposable> lockOne = asyncLock.LockAsync();
            Task<IDisposable> lockTwo = asyncLock.LockAsync();

            Assert.Equal(TaskStatus.RanToCompletion, lockOne.Status);
            Assert.Equal(TaskStatus.WaitingForActivation, lockTwo.Status);
            lockOne.Result.Dispose();

            await lockTwo;
            Assert.Equal(TaskStatus.RanToCompletion, lockTwo.Status);
        }

        [Fact]
        public void AsyncLockCancelsWhenRequested()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            AsyncLock asyncLock = new AsyncLock();

            Task<IDisposable> lockOne = asyncLock.LockAsync();
            Task<IDisposable> lockTwo = asyncLock.LockAsync(cts.Token);

            // Cancel the second lock before the first is released
            cts.Cancel();
            lockOne.Result.Dispose();

            Assert.Equal(TaskStatus.RanToCompletion, lockOne.Status);
            Assert.Equal(TaskStatus.Canceled, lockTwo.Status);
        }
    }
}

