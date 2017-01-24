//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class AsyncDebouncerTests
    {
        [Fact(Skip = "TODO: This test fails in the new build system, need to investigate!")]
        public async Task AsyncDebouncerFlushesAfterInterval()
        {
            TestAsyncDebouncer debouncer = new TestAsyncDebouncer();

            await debouncer.Invoke(1);
            await debouncer.Invoke(2);
            await debouncer.Invoke(3);
            await Task.Delay(TestAsyncDebouncer.Interval + 100);

            // Add a few more items to ensure they are added after the initial interval
            await debouncer.Invoke(4);
            await debouncer.Invoke(5);
            await debouncer.Invoke(6);

            Assert.Equal(new List<int> { 1, 2, 3 }, debouncer.FlushedBuffer);
            Assert.True(
                debouncer.TimeToFlush > 
                TimeSpan.FromMilliseconds(TestAsyncDebouncer.Interval),
                "Debouncer flushed before interval lapsed.");

            // Check for the later items to see if they've been flushed
            await Task.Delay(TestAsyncDebouncer.Interval + 100);
            Assert.Equal(new List<int> { 4, 5, 6 }, debouncer.FlushedBuffer);
        }

        [Fact(Skip = "TODO: This test fails in the new build system, need to investigate!")]
        public async Task AsyncDebouncerRestartsAfterInvoke()
        {
            TestAsyncRestartDebouncer debouncer = new TestAsyncRestartDebouncer();

            // Invoke the debouncer and wait a bit between each
            // invoke to make sure the debouncer isn't flushed
            // until after the last invoke.
            await debouncer.Invoke(1);
            await Task.Delay(TestAsyncRestartDebouncer.Interval - 100);
            await debouncer.Invoke(2);
            await Task.Delay(TestAsyncRestartDebouncer.Interval - 100);
            await debouncer.Invoke(3);
            await Task.Delay(TestAsyncRestartDebouncer.Interval + 100);

            // The only item flushed should be 3 since its interval has lapsed
            Assert.Equal(new List<int> { 3 }, debouncer.FlushedBuffer);
        }
    }

    #region TestAsyncDebouncer

    internal class TestAsyncDebouncer : AsyncDebouncer<int>
    {
        public const int Interval = 1500;

        DateTime? firstInvoke;
        private List<int> invokeBuffer = new List<int>();

        public List<int> FlushedBuffer { get; private set; }

        public TimeSpan TimeToFlush { get; private set; }

        public TestAsyncDebouncer() : base(Interval, false)
        {
        }

        protected override Task OnInvoke(int args)
        {
            if (!this.firstInvoke.HasValue)
            {
                this.firstInvoke = DateTime.Now;
            }

            this.invokeBuffer.Add(args);

            return Task.FromResult(true);
        }

        protected override Task OnFlush()
        {
            // Mark the flush time
            this.TimeToFlush = DateTime.Now - this.firstInvoke.Value;

            // Copy the buffer contents
            this.FlushedBuffer = this.invokeBuffer.ToList();
            this.invokeBuffer.Clear();

            return Task.FromResult(true);
        }
    }

    #endregion

    #region TestAsyncRestartDebouncer

    internal class TestAsyncRestartDebouncer : AsyncDebouncer<int>
    {
        public const int Interval = 300;

        private int lastInvokeInt = -1;

        public List<int> FlushedBuffer { get; } = new List<int>();

        public TestAsyncRestartDebouncer() : base(Interval, true)
        {
        }

        protected override Task OnInvoke(int args)
        {
            this.lastInvokeInt = args;
            return Task.FromResult(true);
        }

        protected override Task OnFlush()
        {
            this.FlushedBuffer.Add(this.lastInvokeInt);

            return Task.FromResult(true);
        }
    }

    #endregion

}

