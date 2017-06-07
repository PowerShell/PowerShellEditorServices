//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Defines the contract for a collection of provider implementations.
    /// </summary>
    public interface IProviderCollection<TProvider> : IEnumerable<TProvider>
        where TProvider : IProvider
    {
        /// <summary>
        /// Adds a provider to the collection.
        /// </summary>
        /// <param name="provider">The provider to be added.</param>
        void Add(TProvider provider);
    }
}
