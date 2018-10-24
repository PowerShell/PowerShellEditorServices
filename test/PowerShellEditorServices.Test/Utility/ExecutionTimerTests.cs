//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class ExecutionTimerTests
    {
        [Fact]
        public async void DoesNotThrowExceptionWhenDisposedOnAnotherThread()
        {
            var timer = ExecutionTimer.Start(Logging.CreateLogger().Build(), "Message");
            await Task.Run(() => timer.Dispose());
        }
    }
}
