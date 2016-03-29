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
        /// The default host ID for PowerShell Editor Services.  Used
        /// for the host-specific profile path if no host ID is specified.
        /// </summary>
        public const string DefaultHostProfileId = "Microsoft.PowerShellEditorServices";

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
        /// <param name="hostId">
        /// The identifier of the host used in the host-specific X_profile.ps1 filename.</param>
        /// <param name="runspace">A runspace used to gather profile path locations.</param>
        public ProfilePaths(
            string hostId,
            Runspace runspace)
        {
            string allUsersPath =
                (string)runspace
                    .SessionStateProxy
                    .PSVariable
                    .Get("PsHome")
                    .Value;

            string currentUserPath =
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "WindowsPowerShell");

            string currentHostProfileName =
                string.Format(
                    "{0}_{1}",
                    hostId,
                    AllHostsProfileName);

            this.AllUsersCurrentHost = Path.Combine(allUsersPath, currentHostProfileName);
            this.CurrentUserCurrentHost = Path.Combine(currentUserPath, currentHostProfileName);
            this.AllUsersAllHosts = Path.Combine(allUsersPath, AllHostsProfileName);
            this.CurrentUserAllHosts = Path.Combine(currentUserPath, AllHostsProfileName);
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
