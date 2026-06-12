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

            return IsSatisfyingAssembly(requiredAssemblyName, AssemblyName.GetAssemblyName(assemblyPath));
        }

        // Internal (rather than private) purely so it can be unit tested with constructed
        // AssemblyName instances; it has no file-system dependency of its own.
        internal static bool IsSatisfyingAssembly(AssemblyName requiredAssemblyName, AssemblyName asmToLoadName)
        {
            // The simple name must match (case-insensitively, as assembly names are).
            if (!string.Equals(asmToLoadName.Name, requiredAssemblyName.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // The candidate must be at least the requested version. We still accept newer
            // versions, since shared framework and $PSHOME assemblies are generally
            // forward-compatible via the runtime's binding.
            if (asmToLoadName.Version < requiredAssemblyName.Version)
            {
                return false;
            }

            // The strong-name identity must match. Previously only the simple name and version
            // were compared, so a same-named assembly with a *different* public key token (i.e.
            // a genuinely different assembly) was treated as a drop-in replacement and would then
            // fail at runtime with a FileLoadException/TypeLoadException. Requiring the public key
            // token to match means we only short-circuit to a $PSHOME/Common assembly that can
            // actually satisfy the reference; otherwise we fall through and let the default load
            // context resolve it with its own (laxer) rules.
            if (!PublicKeyTokensMatch(requiredAssemblyName, asmToLoadName))
            {
                return false;
            }

            // The culture must match so we never substitute a satellite resource assembly for the
            // neutral one (or vice versa).
            return string.Equals(
                asmToLoadName.CultureName ?? string.Empty,
                requiredAssemblyName.CultureName ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool PublicKeyTokensMatch(AssemblyName requiredAssemblyName, AssemblyName candidateAssemblyName)
        {
            byte[] requiredToken = requiredAssemblyName.GetPublicKeyToken();

            // A reference to a non-strong-named assembly imposes no public key token requirement.
            if (requiredToken is null || requiredToken.Length == 0)
            {
                return true;
            }

            byte[] candidateToken = candidateAssemblyName.GetPublicKeyToken();
            if (candidateToken is null || candidateToken.Length != requiredToken.Length)
            {
                return false;
            }

            for (int i = 0; i < requiredToken.Length; i++)
            {
                if (requiredToken[i] != candidateToken[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
