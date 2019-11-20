using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public class HostInfo
    {
        public HostInfo(string name, string profileId, Version version)
        {
            Name = name;
            ProfileId = profileId;
            Version = version;
        }

        public string Name { get; }

        public string ProfileId { get; }

        public Version Version { get; }
    }
}
