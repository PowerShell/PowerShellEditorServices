//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Defines the contract for a feature provider, particularly for provider identification.
    /// </summary>
    public interface IProvider
    {
        /// <summary>
        /// Specifies a unique identifier for a provider, typically a
        /// fully-qualified name like "Microsoft.PowerShell.EditorServices.MyProvider"
        /// </summary>
        string ProviderId { get; }
    }
}
