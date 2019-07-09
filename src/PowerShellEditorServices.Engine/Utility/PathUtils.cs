using System;

namespace PowerShellEditorServices.Engine.Utility
{
    internal class PathUtils
    {
        public string WildcardUnescapePath(string path)
        {
            throw new NotImplementedException();
        }

        public static Uri ToUri(string fileName)
        {
            fileName = fileName.Replace(":", "%3A").Replace("\\", "/");
            if (!fileName.StartsWith("/")) return new Uri($"file:///{fileName}");
            return new Uri($"file://{fileName}");
        }

        public static string FromUri(Uri uri)
        {
            if (uri.Segments.Length > 1)
            {
                // On windows of the Uri contains %3a local path
                // doesn't come out as a proper windows path
                if (uri.Segments[1].IndexOf("%3a", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return FromUri(new Uri(uri.AbsoluteUri.Replace("%3a", ":").Replace("%3A", ":")));
                }
            }
            return uri.LocalPath;
        }
    }
}
