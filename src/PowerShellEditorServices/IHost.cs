//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using System;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides an interface for starting and identifying a host.
    /// </summary>
    public interface IHost
    {
        /// <summary>
        /// Gets the host application's identifying name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the host application's version number.
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Starts the host's message pump.
        /// </summary>
        void Start();
    }
}
