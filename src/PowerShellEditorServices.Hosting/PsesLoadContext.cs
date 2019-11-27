//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// An AssemblyLoadContext (ALC) designed to find PSES' dependencies in the given directory.
    /// This class only exists in .NET Core, where the ALC is used to isolate PSES' dependencies
    /// from the PowerShell assembly load context so that modules can import their own dependencies
    /// without issue in PSES.
    /// </summary>
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
                return LoadFromAssemblyPath(asmPath);
            }

            return null;
        }
    }
}
