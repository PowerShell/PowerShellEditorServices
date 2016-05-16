//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Provides profile path resolution behavior relative to the name
    /// of a particular PowerShell host.
    /// </summary>
    public class ProfilePaths
    {
        #region Constants

        /// <summary>
        /// The file name for the "all hosts" profile.  Also used as the
        /// suffix for the host-specific profile filenames.
        /// </summary>
        public const string AllHostsProfileName = "profile.ps1";

        #endregion

        #region Properties

        /// <summary>
        /// Gets the profile path for all users, all hosts.
        /// </summary>
        public string AllUsersAllHosts { get; private set; }

        /// <summary>
        /// Gets the profile path for all users, current host.
        /// </summary>
        public string AllUsersCurrentHost { get; private set; }

        /// <summary>
        /// Gets the profile path for the current user, all hosts.
        /// </summary>
        public string CurrentUserAllHosts { get; private set; }

        /// <summary>
        /// Gets the profile path for the current user and host.
        /// </summary>
        public string CurrentUserCurrentHost { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new instance of the ProfilePaths class.
        /// </summary>
        /// <param name="hostProfileId">
        /// The identifier of the host used in the host-specific X_profile.ps1 filename.
        /// </param>
        /// <param name="baseAllUsersPath">The base path to use for constructing AllUsers profile paths.</param>
        /// <param name="baseCurrentUserPath">The base path to use for constructing CurrentUser profile paths.</param>
        public ProfilePaths(
            string hostProfileId,
            string baseAllUsersPath,
            string baseCurrentUserPath)
        {
            this.Initialize(hostProfileId, baseAllUsersPath, baseCurrentUserPath);
        }

        private void Initialize(
            string hostProfileId,
            string baseAllUsersPath,
            string baseCurrentUserPath)
        {
            string currentHostProfileName =
                string.Format(
                    "{0}_{1}",
                    hostProfileId,
                    AllHostsProfileName);

            this.AllUsersCurrentHost = Path.Combine(baseAllUsersPath, currentHostProfileName);
            this.CurrentUserCurrentHost = Path.Combine(baseCurrentUserPath, currentHostProfileName);
            this.AllUsersAllHosts = Path.Combine(baseAllUsersPath, AllHostsProfileName);
            this.CurrentUserAllHosts = Path.Combine(baseCurrentUserPath, AllHostsProfileName);
        }

        /// <summary>
        /// Gets the list of profile paths that exist on the filesystem.
        /// </summary>
        /// <returns>An IEnumerable of profile path strings to be loaded.</returns>
        public IEnumerable<string> GetLoadableProfilePaths()
        {
            var profilePaths =
                new string[]
                {
                    this.AllUsersAllHosts,
                    this.AllUsersCurrentHost,
                    this.CurrentUserAllHosts,
                    this.CurrentUserCurrentHost
                };

            return profilePaths.Where(p => File.Exists(p));
        }

        #endregion
    }
}

