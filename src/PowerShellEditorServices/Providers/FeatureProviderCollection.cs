//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a default implementation of IFeatureProviderCollection.
    /// </summary>
    public class FeatureProviderCollection<TProvider> : IFeatureProviderCollection<TProvider>
        where TProvider : IFeatureProvider
    {
        #region Private Fields

        private List<TProvider> providerList = new List<TProvider>();

        #endregion

        #region IFeatureProviderCollection Implementation

        void IFeatureProviderCollection<TProvider>.Add(TProvider provider)
        {
            if (!this.providerList.Contains(provider))
            {
                this.providerList.Add(provider);
            }
        }

        IEnumerator<TProvider> IEnumerable<TProvider>.GetEnumerator()
        {
            return this.providerList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.providerList.GetEnumerator();
        }

        #endregion
    }
}