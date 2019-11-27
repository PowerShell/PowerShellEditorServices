//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

#if CoreCLR
using System.Runtime.Loader;
#endif

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Class to contain the loading behavior of Editor Services.
    /// In particular, this class wraps the point where Editor Services is safely loaded
    /// in a way that separates its dependencies from the calling context.
    /// </summary>
    public sealed class EditorServicesLoader : IDisposable
    {
        private const int Net461Version = 394254;

        private static readonly string s_psesDependencyDirPath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..",
                "Common"));

#if CoreCLR
        private static readonly AssemblyLoadContext s_coreAsmLoadContext = new PsesLoadContext(s_psesDependencyDirPath);
#endif

        /// <summary>
        /// Create a new Editor Services loader.
        /// </summary>
        /// <param name="logger">The host logger to use.</param>
        /// <param name="hostConfig">The host configuration to start editor services with.</param>
        /// <param name="sessionFileWriter">The session file writer to write the session file with.</param>
        /// <returns></returns>
        public static EditorServicesLoader Create(
            HostLogger logger,
            EditorServicesConfig hostConfig,
            ISessionFileWriter sessionFileWriter,
            IReadOnlyCollection<IDisposable> loggersToUnsubscribe = null)
        {
#if CoreCLR
            // In .NET Core, we add an event here to redirect dependency loading to the new AssemblyLoadContext we load PSES' dependencies into
            logger.Log(PsesLogLevel.Verbose, "Adding AssemblyResolve event handler for new AssemblyLoadContext dependency loading");
            AssemblyLoadContext.Default.Resolving += DefaultLoadContext_OnAssemblyResolve;
#else
            // In .NET Framework we add an event here to redirect dependency loading in the current AppDomain for PSES' dependencies
            logger.Log(PsesLogLevel.Verbose, "Adding AssemblyResolve event handler for dependency loading");
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_OnAssemblyResolve;
#endif

            return new EditorServicesLoader(logger, hostConfig, sessionFileWriter, loggersToUnsubscribe);
        }

        private readonly EditorServicesConfig _hostConfig;

        private readonly ISessionFileWriter _sessionFileWriter;

        private readonly HostLogger _logger;

        private readonly IReadOnlyCollection<IDisposable> _loggersToUnsubscribe;

        private EditorServicesLoader(
            HostLogger logger,
            EditorServicesConfig hostConfig,
            ISessionFileWriter sessionFileWriter,
            IReadOnlyCollection<IDisposable> loggersToUnsubscribe)
        {
            _logger = logger;
            _hostConfig = hostConfig;
            _sessionFileWriter = sessionFileWriter;
            _loggersToUnsubscribe = loggersToUnsubscribe;
        }

        /// <summary>
        /// Load Editor Services and its dependencies in an isolated way and start it.
        /// This method's returned task will end when Editor Services shuts down.
        /// </summary>
        /// <returns></returns>
        public async Task LoadAndRunEditorServicesAsync()
        {
            // Method with no implementation that forces the PSES assembly to load, triggering an AssemblyResolve event
            _logger.Log(PsesLogLevel.Verbose, "Loading PSES assemblies");
            EditorServicesLoading.LoadEditorServicesForHost();

            _logger.Log(PsesLogLevel.Verbose, "Starting EditorServices");
            using (var editorServicesRunner = EditorServicesRunner.Create(_logger, _hostConfig, _sessionFileWriter, _loggersToUnsubscribe))
            {
                // The trigger method for Editor Services
                // We will wait here until Editor Services shuts down
                await editorServicesRunner.RunUntilShutdown().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            // TODO: Deregister assembly event
        }

#if CoreCLR
        private static Assembly DefaultLoadContext_OnAssemblyResolve(AssemblyLoadContext defaultLoadContext, AssemblyName asmName)
        {
            // We only want the Editor Services DLL; the new ALC will lazily load its dependencies automatically
            if (!string.Equals(asmName.Name, "Microsoft.PowerShell.EditorServices", StringComparison.Ordinal))
            {
                return null;
            }

            string asmPath = Path.Combine(s_psesDependencyDirPath, $"{asmName.Name}.dll");

            return s_coreAsmLoadContext.LoadFromAssemblyPath(asmPath);
        }
#endif

#if !CoreCLR
        private static Assembly CurrentDomain_OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var asmName = new AssemblyName(args.Name);

            string asmPath = Path.Combine(s_psesDependencyDirPath, $"{asmName.Name}.dll");
            
            return File.Exists(asmPath)
                ? Assembly.LoadFrom(asmPath)
                : null;
        }
#endif

    }
}
