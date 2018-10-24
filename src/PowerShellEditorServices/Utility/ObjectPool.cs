//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// A basic implementation of the object pool pattern.
    /// </summary>
    internal class ObjectPool<T>
        where T : new()
    {
        private ConcurrentBag<T> _pool = new ConcurrentBag<T>();

        /// <summary>
        /// Get an instance of an object, either new or from the pool depending on availability.
        /// </summary>
        public T Rent() => _pool.TryTake(out var obj) ? obj : new T();

        /// <summary>
        /// Return an object to the pool.
        /// </summary>
        /// <param name="obj">The object to return to the pool.</param>
        public void Return(T obj) => _pool.Add(obj);
    }
}
