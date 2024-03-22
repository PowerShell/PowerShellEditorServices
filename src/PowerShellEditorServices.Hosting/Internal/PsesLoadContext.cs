// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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
        private static readonly string s_psHome = Path.GetDirectoryName(
            Assembly.GetEntryAssembly().Location);

        private readonly string _dependencyDirPath;

        public PsesLoadContext(string dependencyDirPath)
        {
            _dependencyDirPath = dependencyDirPath;

            // Try and set our name in .NET Core 3+ for logging niceness
            TrySetName("PsesLoadContext");
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Since this class is responsible for loading any DLLs in .NET Core,
            // we must restrict the code in here to only use core types,
            // otherwise we may depend on assembly that we are trying to load and cause a StackOverflowException

            // If we find the required assembly in $PSHOME, let another mechanism load the assembly
            string psHomeAsmPath = Path.Join(s_psHome, $"{assemblyName.Name}.dll");
            if (IsSatisfyingAssembly(assemblyName, psHomeAsmPath))
            {
                return null;
            }

            string asmPath = Path.Join(_dependencyDirPath, $"{assemblyName.Name}.dll");
            if (IsSatisfyingAssembly(assemblyName, asmPath))
            {
                return LoadFromAssemblyPath(asmPath);
            }

            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort; we must not throw if we fail")]
        private void TrySetName(string name)
        {
            try
            {
                // This field only exists in .NET Core 3+, but helps logging
                FieldInfo nameBackingField = typeof(AssemblyLoadContext).GetField(
                    "_name",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                nameBackingField?.SetValue(this, name);
            }
            catch
            {
                // Do nothing -- we did our best
            }
        }

        private static bool IsSatisfyingAssembly(AssemblyName requiredAssemblyName, string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
            {
                return false;
            }

            AssemblyName asmToLoadName = AssemblyName.GetAssemblyName(assemblyPath);

            return string.Equals(asmToLoadName.Name, requiredAssemblyName.Name, StringComparison.OrdinalIgnoreCase)
                && asmToLoadName.Version >= requiredAssemblyName.Version;
        }
    }
}
