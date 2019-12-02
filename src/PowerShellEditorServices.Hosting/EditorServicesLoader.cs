//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using SMA = System.Management.Automation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

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

            var psesLoadContext = new PsesLoadContext(logger, s_psesDependencyDirPath);

            AssemblyLoadContext.Default.Resolving += (AssemblyLoadContext defaultLoadContext, AssemblyName asmName) =>
            {
                logger.Log(PsesLogLevel.Diagnostic, $"Assembly resolve event fired for {asmName}");

                // We only want the Editor Services DLL; the new ALC will lazily load its dependencies automatically
                if (!string.Equals(asmName.Name, "Microsoft.PowerShell.EditorServices", StringComparison.Ordinal))
                {
                    return null;
                }

                string asmPath = Path.Combine(s_psesDependencyDirPath, $"{asmName.Name}.dll");

                logger.Log(PsesLogLevel.Verbose, "Loading PSES DLL using new assembly load context");

                return psesLoadContext.LoadFromAssemblyPath(asmPath);
            };
#else
            // In .NET Framework we add an event here to redirect dependency loading in the current AppDomain for PSES' dependencies
            logger.Log(PsesLogLevel.Verbose, "Adding AssemblyResolve event handler for dependency loading");
            AppDomain.CurrentDomain.AssemblyResolve += (object sender, ResolveEventArgs args) =>
            {
                logger.Log(PsesLogLevel.Diagnostic, $"Assembly resolve event fired for {args.Name}");

                var asmName = new AssemblyName(args.Name);

                string asmPath = Path.Combine(s_psesDependencyDirPath, $"{asmName.Name}.dll");
                
                if (!File.Exists(asmPath))
                {
                    return null;
                }

                logger.Log(PsesLogLevel.Diagnostic, $"Loading {args.Name} from PSES dependency dir into LoadFrom context");
                return Assembly.LoadFrom(asmPath);
            };
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
#if !CoreCLR
            // Make sure the .NET Framework version supports .NET Standard 2.0
            CheckNetFxVersion();
#endif

            // Ensure the language mode allows us to run
            CheckLanguageMode();

            // Add the bundled modules to the PSModulePath
            UpdatePSModulePath();

            // Log important host information here
            LogHostInformation();

            // Check to see if the configuration we have is valid
            ValidateConfiguration();

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
            // TODO: Remove assembly resolve events
            //       This is not high priority, since the PSES process shouldn't be reused
        }

#if !CoreCLR
        private void CheckNetFxVersion()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Checking that .NET Framework version is at least 4.6.1");
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Net Framework Setup\NDP\v4\Full"))
            {
                object netFxValue = key?.GetValue("Release");
                if (netFxValue == null || !(netFxValue is int netFxVersion))
                {
                    return;
                }

                if (netFxVersion < Net461Version)
                {
                    _logger.Log(PsesLogLevel.Warning, $".NET Framework version {netFxVersion} lower than .NET 4.6.1. This runtime is not supported and you may experience errors. Please update your .NET runtime version.");
                }
            }
        }
#endif

        /// <summary>
        /// PSES currently does not work in Constrained Language Mode, because PSReadLine script invocations won't work in it.
        /// Ideally we can find a better way so that PSES will work in CLM.
        /// </summary>
        private void CheckLanguageMode()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Checking that PSES is running in FullLanguage mode");
            using (var pwsh = SMA.PowerShell.Create())
            {
                if (pwsh.Runspace.SessionStateProxy.LanguageMode != SMA.PSLanguageMode.FullLanguage)
                {
                    throw new InvalidOperationException("Cannot start PowerShell Editor Services in Constrained Language Mode");
                }
            }
        }

        private void UpdatePSModulePath()
        {
            if (string.IsNullOrEmpty(_hostConfig.BundledModulePath))
            {
                _logger.Log(PsesLogLevel.Diagnostic, "BundledModulePath not set, skipping");
                return;
            }

            string psModulePath = Environment.GetEnvironmentVariable("PSModulePath").TrimEnd(Path.PathSeparator);
            psModulePath = $"{psModulePath}{Path.PathSeparator}{_hostConfig.BundledModulePath}";
            Environment.SetEnvironmentVariable("PSModulePath", psModulePath);

            _logger.Log(PsesLogLevel.Verbose, $"Updated PSModulePath to: '{psModulePath}'");
        }

        private void LogHostInformation()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Logging host information");
            _logger.Log(PsesLogLevel.Verbose, $@"
== Host Startup Configuration Details ==
 - Host name:                 {_hostConfig.HostInfo.Name}
 - Host version:              {_hostConfig.HostInfo.Version}
 - Host profile ID:           {_hostConfig.HostInfo.ProfileId}
 - PowerShell host type:      {_hostConfig.PSHost.GetType()}

 - REPL setting:              {_hostConfig.ConsoleRepl}
 - Session details path:      {_hostConfig.SessionDetailsPath}
 - Bundled modules path:      {_hostConfig.BundledModulePath}
 - Additional modules:        {_hostConfig.AdditionalModules}
 - Feature flags:             {_hostConfig.FeatureFlags}

 - Log path:                  {_hostConfig.LogPath}
 - Minimum log level:         {_hostConfig.LogLevel}

 - Profile paths:
   + AllUsersAllHosts:       {_hostConfig.ProfilePaths.AllUsersAllHosts ?? "<null>"}
   + AllUsersCurrentHost:    {_hostConfig.ProfilePaths.AllUsersCurrentHost ?? "<null>"}
   + CurrentUserAllHosts:    {_hostConfig.ProfilePaths.CurrentUserAllHosts ?? "<null>"}
   + CurrentUserCurrentHost: {_hostConfig.ProfilePaths.CurrentUserCurrentHost ?? "<null>"}
");

            _logger.Log(PsesLogLevel.Verbose, $@"
== Console Details ==
 - Console input encoding: {Console.InputEncoding.EncodingName}
 - Console output encoding: {Console.OutputEncoding.EncodingName}
 - PowerShell output encoding: {GetPSOutputEncoding()}
");

            LogOperatingSystemDetails();
        }

        private string GetPSOutputEncoding()
        {
            using (var pwsh = SMA.PowerShell.Create())
            {
                return pwsh.AddScript("$OutputEncoding.EncodingName").Invoke<string>()[0];
            }
        }

        private void LogOperatingSystemDetails()
        {
            _logger.Log(PsesLogLevel.Verbose, $@"
== Environment Details ==
 - OS description: {RuntimeInformation.OSDescription}
 - OS architecture: {GetOSArchitecture()}
 - Process bitness: {(Environment.Is64BitProcess ? "64" : "32")}
");
        }

        private string GetOSArchitecture()
        {
#if CoreCLR
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return RuntimeInformation.OSArchitecture.ToString();
            }
#endif

            // If on win7 (version 6.1.x), avoid System.Runtime.InteropServices.RuntimeInformation
            if (Environment.OSVersion.Version < new Version(6, 2))
            {
                return Environment.Is64BitProcess
                    ? "X64"
                    : "X86";
            }

            return RuntimeInformation.OSArchitecture.ToString();
        }

        private void ValidateConfiguration()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Validating configuration");

            bool lspUsesStdio = _hostConfig.LanguageServiceTransport is StdioTransportConfig;
            bool debugUsesStdio = _hostConfig.DebugServiceTransport is StdioTransportConfig;

            // Ensure LSP and Debug are not both Stdio
            if (lspUsesStdio && debugUsesStdio)
            {
                throw new ArgumentException("LSP and Debug transports cannot both use Stdio");
            }

            if (_hostConfig.ConsoleRepl != ConsoleReplKind.None
                && (lspUsesStdio || debugUsesStdio))
            {
                throw new ArgumentException("Cannot use the REPL with a Stdio protocol transport");
            }

            if (_hostConfig.PSHost == null)
            {
                throw new ArgumentNullException(nameof(_hostConfig.PSHost));
            }
        }
    }
}
