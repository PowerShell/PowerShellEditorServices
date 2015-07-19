using System.IO;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public static class ResourceFileLoader
    {
        public static TextReader LoadFileFromResource(
            string fileName, 
            Assembly resourceAssembly)
        {
            using (Stream stream = resourceAssembly.GetManifestResourceStream(fileName))
            using (StreamReader reader = new StreamReader(stream))
            {
                // Read the file contents into a StringReader so that
                // we can dispose the resource stream before returning
                return new StringReader(reader.ReadToEnd());
            }
        }

        public static TextReader LoadFileFromResource(string fileName)
        {
            // Load the file from the current assembly
            return
                LoadFileFromResource(
                    fileName,
                    Assembly.GetExecutingAssembly());
        }
    }
}
