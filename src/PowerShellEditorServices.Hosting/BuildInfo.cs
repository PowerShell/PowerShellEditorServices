using System.Globalization;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public static class BuildInfo
    {
        // TODO: Include a Git commit hash in this.
        public static readonly string BuildVersion = "<development-build>";
        public static readonly string BuildOrigin = "<development>";
        public static readonly System.DateTime? BuildTime = System.DateTime.Parse("2020-05-03T16:39:35", CultureInfo.InvariantCulture.DateTimeFormat);
    }
}
