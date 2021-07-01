
namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal struct HostStartOptions
    {
        public bool LoadProfiles { get; set; }

        public string InitialWorkingDirectory { get; set; }
    }
}
