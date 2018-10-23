//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class ObjectPoolTests
    {
        [Fact]
        public void DoesNotCreateNewObjects()
        {
            var pool = new ObjectPool<object>();
            var obj = pool.Rent();
            pool.Return(obj);

            Assert.Same(obj, pool.Rent());
        }
    }
}
