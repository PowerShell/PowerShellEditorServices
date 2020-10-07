using System.Globalization;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public static class BuildInfo
    {
        public static readonly string BuildVersion = "<development-build>";
        public static readonly string BuildOrigin = "Development";
        public static readonly System.DateTime? BuildTime = System.DateTime.Parse("2020-10-07T03:25:20", CultureInfo.InvariantCulture.DateTimeFormat);
    }
}
