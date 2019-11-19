using Microsoft.PowerShell.EditorServices.Hosting;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PowerShellEditorServices.Hosting
{
    public class EditorServicesLoader : IDisposable
    {
        private const int Net461Version = 394254;

        private static readonly string s_dependencyPath = null;

        public static EditorServicesLoader Create(EditorServicesConfig hostConfig, string dependencyPath = null)
        {
            // TODO: Check host config transport configs

            if (hostConfig.ProfilePaths == null)
            {
                hostConfig.ProfilePaths = GetProfilePaths(hostConfig.HostDetails.ProfileId);
            }

            return new EditorServicesLoader(hostConfig);
        }

        private readonly EditorServicesConfig _hostConfig;

        public EditorServicesLoader(EditorServicesConfig hostConfig)
        {
            _hostConfig = hostConfig;
        }

        public async Task LoadAndRunEditorServicesAsync()
        {
        }

        public void Dispose()
        {
        }

        private static ProfilePaths GetProfilePaths(string profileId)
        {
            Collection<string> profileLocations = null;
            using (var pwsh = PowerShell.Create())
            {
                profileLocations = pwsh.AddScript("$profile.AllUsersAllHosts,$profile.CurrentUserAllHosts").Invoke<string>();
            }

            return new ProfilePaths(
                profileId,
                baseAllUsersPath: Path.GetDirectoryName(profileLocations[0]),
                baseCurrentUserPath: Path.GetDirectoryName(profileLocations[1]));
        }
    }
}
