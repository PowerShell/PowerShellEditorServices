using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    internal class PsesLoadContext : AssemblyLoadContext
    {
        private readonly string _dependencyDirPath;

        public PsesLoadContext(string dependencyDirPath)
        {
            _dependencyDirPath = dependencyDirPath;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string asmPath = Path.Combine(_dependencyDirPath, $"{assemblyName.Name}.dll");

            if (File.Exists(asmPath))
            {
                //Console.WriteLine($"Loading {assemblyName} in PSES load context");
                return LoadFromAssemblyPath(asmPath);
            }

            //Console.WriteLine($"Failed to load {assemblyName} in PSES load context");
            return null;
        }
    }
}
