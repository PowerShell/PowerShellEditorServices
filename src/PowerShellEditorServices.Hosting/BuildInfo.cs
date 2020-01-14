using System.Globalization;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public static class BuildInfo
    {
        public static readonly string BuildVersion = "<development-build>";
        public static readonly string BuildOrigin = "<development>";
        public static readonly System.DateTime? BuildTime = System.DateTime.Parse("2019-12-06T21:43:41", CultureInfo.InvariantCulture.DateTimeFormat);
    }
}
