//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// A simple readonly object to describe basic host metadata.
    /// </summary>
    public class HostInfo
    {
        /// <summary>
        /// Create a new host info object.
        /// </summary>
        /// <param name="name">The name of the host.</param>
        /// <param name="profileId">The profile ID of the host.</param>
        /// <param name="version">The version of the host.</param>
        public HostInfo(string name, string profileId, Version version)
        {
            Name = name;
            ProfileId = profileId;
            Version = version;
        }

        /// <summary>
        /// The name of the host.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The profile ID of the host.
        /// </summary>
        public string ProfileId { get; }

        /// <summary>
        /// The version of the host.
        /// </summary>
        public Version Version { get; }
    }
}
