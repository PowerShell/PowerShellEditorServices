//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    public class ObjectPool<T>
        where T : new()
    {
        private ConcurrentBag<T> _pool = new ConcurrentBag<T>();

        public T Rent() => _pool.TryTake(out var obj) ? obj : new T();

        public void Return(T obj) => _pool.Add(obj);
    }
}
