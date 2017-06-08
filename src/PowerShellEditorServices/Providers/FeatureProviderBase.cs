//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a base implementation of IFeatureProvider.
    /// </summary>
    public abstract class FeatureProviderBase : IFeatureProvider
    {
        /// <summary>
        /// Gets the provider class type's FullName as the
        /// ProviderId.
        /// </summary>
        public string ProviderId => this.GetType().FullName;
    }
}
