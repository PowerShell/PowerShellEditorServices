﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SMA = System.Management.Automation;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

#if ASSEMBLY_LOAD_STACKTRACE
using System.Diagnostics;
#endif

#if CoreCLR
using System.Runtime.Loader;
#else
using Microsoft.Win32;
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
#if !CoreCLR
        // TODO: Well, we're saying we need 4.8 here but we're building for 4.6.2...
        // See https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
        private const int Net48Version = 528040;

        private static readonly string s_psesBaseDirPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#endif

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
        /// <param name="sessionDetailsPath">Path to the session file to create on startup or startup failure.</param>
        /// <param name="loggersToUnsubscribe">The loggers to unsubscribe form writing to the terminal.</param>
        public static EditorServicesLoader Create(
            HostLogger logger,
            EditorServicesConfig hostConfig,
            string sessionDetailsPath,
            IReadOnlyCollection<IDisposable> loggersToUnsubscribe)
        {
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (hostConfig is null)
            {
                throw new ArgumentNullException(nameof(hostConfig));
            }

            Version powerShellVersion = GetPSVersion();
            SessionFileWriter sessionFileWriter = new(logger, sessionDetailsPath, powerShellVersion);
            logger.Log(PsesLogLevel.Diagnostic, "Session file writer created");

#if CoreCLR
            // In .NET Core, we add an event here to redirect dependency loading to the new AssemblyLoadContext we load PSES' dependencies into
            logger.Log(PsesLogLevel.Verbose, "Adding AssemblyResolve event handler for new AssemblyLoadContext dependency loading");

            PsesLoadContext psesLoadContext = new(s_psesDependencyDirPath);

            if (hostConfig.LogLevel == PsesLogLevel.Diagnostic)
            {
                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    logger.Log(
                        PsesLogLevel.Diagnostic,
                        $"Loaded into load context {AssemblyLoadContext.GetLoadContext(args.LoadedAssembly)}: {args.LoadedAssembly}");
                };
            }

            AssemblyLoadContext.Default.Resolving += (AssemblyLoadContext _, AssemblyName asmName) =>
            {
#if ASSEMBLY_LOAD_STACKTRACE
                logger.Log(PsesLogLevel.Diagnostic, $"Assembly resolve event fired for {asmName}. Stacktrace:\n{new StackTrace()}");
#else
                logger.Log(PsesLogLevel.Diagnostic, $"Assembly resolve event fired for {asmName}");
#endif

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

            if (hostConfig.LogLevel == PsesLogLevel.Diagnostic)
            {
                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    if (args.LoadedAssembly.IsDynamic)
                    {
                        return;
                    }

                    logger.Log(
                        PsesLogLevel.Diagnostic,
                        $"Loaded '{args.LoadedAssembly.GetName()}' from '{args.LoadedAssembly.Location}'");
                };
            }

            // Unlike in .NET Core, we need to be look for all dependencies in .NET Framework, not just PSES.dll
            AppDomain.CurrentDomain.AssemblyResolve += (object sender, ResolveEventArgs args) =>
            {
#if ASSEMBLY_LOAD_STACKTRACE
                logger.Log(PsesLogLevel.Diagnostic, $"Assembly resolve event fired for {args.Name}. Stacktrace:\n{new StackTrace()}");
#else
                logger.Log(PsesLogLevel.Diagnostic, $"Assembly resolve event fired for {args.Name}");
#endif

                AssemblyName asmName = new(args.Name);
                string dllName = $"{asmName.Name}.dll";

                // First look for the required assembly in the .NET Framework DLL dir
                string baseDirAsmPath = Path.Combine(s_psesBaseDirPath, dllName);
                if (File.Exists(baseDirAsmPath))
                {
                    logger.Log(PsesLogLevel.Diagnostic, $"Loading {args.Name} from PSES base dir into LoadFile context");
                    return Assembly.LoadFile(baseDirAsmPath);
                }

                // Then look in the shared .NET Standard directory
                string asmPath = Path.Combine(s_psesDependencyDirPath, dllName);
                if (File.Exists(asmPath))
                {
                    logger.Log(PsesLogLevel.Diagnostic, $"Loading {args.Name} from PSES dependency dir into LoadFile context");
                    return Assembly.LoadFile(asmPath);
                }

                return null;
            };
#endif

            return new EditorServicesLoader(logger, hostConfig, sessionFileWriter, loggersToUnsubscribe, powerShellVersion);
        }

        private readonly EditorServicesConfig _hostConfig;

        private readonly ISessionFileWriter _sessionFileWriter;

        private readonly HostLogger _logger;

        private readonly IReadOnlyCollection<IDisposable> _loggersToUnsubscribe;

        private readonly Version _powerShellVersion;

        private EditorServicesRunner _editorServicesRunner;

        private EditorServicesLoader(
            HostLogger logger,
            EditorServicesConfig hostConfig,
            ISessionFileWriter sessionFileWriter,
            IReadOnlyCollection<IDisposable> loggersToUnsubscribe,
            Version powerShellVersion)
        {
            _logger = logger;
            _hostConfig = hostConfig;
            _sessionFileWriter = sessionFileWriter;
            _loggersToUnsubscribe = loggersToUnsubscribe;
            _powerShellVersion = powerShellVersion;
        }

        /// <summary>
        /// Load Editor Services and its dependencies in an isolated way and start it.
        /// This method's returned task will end when Editor Services shuts down.
        /// </summary>
        public Task LoadAndRunEditorServicesAsync()
        {
            // Log important host information here
            LogHostInformation();

            CheckPowerShellVersion();

#if !CoreCLR
            // Make sure the .NET Framework version supports .NET Standard 2.0
            CheckDotNetVersion();
#endif

            // Add the bundled modules to the PSModulePath
            // TODO: Why do we do this in addition to passing the bundled module path to the host?
            UpdatePSModulePath();

            // Check to see if the configuration we have is valid
            ValidateConfiguration();

            // Method with no implementation that forces the PSES assembly to load, triggering an AssemblyResolve event
            _logger.Log(PsesLogLevel.Verbose, "Loading PowerShell Editor Services");
            LoadEditorServices();

            _logger.Log(PsesLogLevel.Verbose, "Starting EditorServices");

            _editorServicesRunner = new EditorServicesRunner(_logger, _hostConfig, _sessionFileWriter, _loggersToUnsubscribe);

            // The trigger method for Editor Services
            return Task.Run(_editorServicesRunner.RunUntilShutdown);
        }

        public void Dispose()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Loader disposed");
            _editorServicesRunner?.Dispose();

            // TODO:
            // Remove assembly resolve events
            // This is not high priority, since the PSES process shouldn't be reused
        }

        private static void LoadEditorServices() =>
            // This must be in its own method, since the actual load happens when the calling method is called
            // The call within this method is therefore a total no-op
            EditorServicesLoading.LoadEditorServicesForHost();

        private void CheckPowerShellVersion()
        {
            PSLanguageMode languageMode = Runspace.DefaultRunspace.SessionStateProxy.LanguageMode;

            _logger.Log(PsesLogLevel.Verbose, $@"
== PowerShell Details ==
- PowerShell version: {_powerShellVersion}
- Language mode:      {languageMode}
");

            if ((_powerShellVersion < new Version(5, 1))
                || (_powerShellVersion >= new Version(6, 0) && _powerShellVersion < new Version(7, 2)))
            {
                _logger.Log(PsesLogLevel.Error, $"PowerShell {_powerShellVersion} is not supported, please update!");
                _sessionFileWriter.WriteSessionFailure("powerShellVersion");
            }

            // TODO: Check if language mode still matters for support.
        }

#if !CoreCLR
        private void CheckDotNetVersion()
        {
            _logger.Log(PsesLogLevel.Verbose, "Checking that .NET Framework version is at least 4.8");
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Net Framework Setup\NDP\v4\Full");
            object netFxValue = key?.GetValue("Release");
            if (netFxValue == null || netFxValue is not int netFxVersion)
            {
                return;
            }

            _logger.Log(PsesLogLevel.Verbose, $".NET registry version: {netFxVersion}");

            if (netFxVersion < Net48Version)
            {
                _logger.Log(PsesLogLevel.Error, $".NET Framework {netFxVersion} is out-of-date, please install at least 4.8: https://dotnet.microsoft.com/en-us/download/dotnet-framework");
                _sessionFileWriter.WriteSessionFailure("dotNetVersion");
            }
        }
#endif

        private void UpdatePSModulePath()
        {
            if (string.IsNullOrEmpty(_hostConfig.BundledModulePath))
            {
                _logger.Log(PsesLogLevel.Diagnostic, "BundledModulePath not set, skipping");
                return;
            }

            string psModulePath = Environment.GetEnvironmentVariable("PSModulePath").TrimEnd(Path.PathSeparator);
            if ($"{psModulePath}{Path.PathSeparator}".Contains($"{_hostConfig.BundledModulePath}{Path.PathSeparator}"))
            {
                _logger.Log(PsesLogLevel.Diagnostic, "BundledModulePath already set, skipping");
                return;
            }
            psModulePath = $"{psModulePath}{Path.PathSeparator}{_hostConfig.BundledModulePath}";
            Environment.SetEnvironmentVariable("PSModulePath", psModulePath);
            _logger.Log(PsesLogLevel.Verbose, $"Updated PSModulePath to: '{psModulePath}'");
        }

        private void LogHostInformation()
        {
            _logger.Log(PsesLogLevel.Verbose, $"PID: {System.Diagnostics.Process.GetCurrentProcess().Id}");

            _logger.Log(PsesLogLevel.Verbose, $@"
== Build Details ==
- Editor Services version: {BuildInfo.BuildVersion}
- Build origin:            {BuildInfo.BuildOrigin}
- Build commit:            {BuildInfo.BuildCommit}
- Build time:              {BuildInfo.BuildTime}
");

            _logger.Log(PsesLogLevel.Verbose, $@"
== Host Startup Configuration Details ==
 - Host name:                 {_hostConfig.HostInfo.Name}
 - Host version:              {_hostConfig.HostInfo.Version}
 - Host profile ID:           {_hostConfig.HostInfo.ProfileId}
 - PowerShell host type:      {_hostConfig.PSHost.GetType()}

 - REPL setting:              {_hostConfig.ConsoleRepl}
 - Session details path:      {_hostConfig.SessionDetailsPath}
 - Bundled modules path:      {_hostConfig.BundledModulePath}
 - Additional modules:        {(_hostConfig.AdditionalModules == null ? "<null>" : string.Join(", ", _hostConfig.AdditionalModules))}
 - Feature flags:             {(_hostConfig.FeatureFlags == null ? "<null>" : string.Join(", ", _hostConfig.FeatureFlags))}

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

            _logger.Log(PsesLogLevel.Verbose, $@"
== Environment Details ==
 - OS description:  {RuntimeInformation.OSDescription}
 - OS architecture: {RuntimeInformation.OSArchitecture}
 - Process bitness: {(Environment.Is64BitProcess ? "64" : "32")}
");
        }

        private static string GetPSOutputEncoding()
        {
            using SMA.PowerShell pwsh = SMA.PowerShell.Create();
            return pwsh.AddScript(
                "[System.Diagnostics.DebuggerHidden()]param() $OutputEncoding.EncodingName",
                useLocalScope: true).Invoke<string>()[0];
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Checking user-defined configuration")]
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1825:Avoid zero-length array allocations", Justification = "Cannot use Array.Empty, since it must work in net452")]
        private static Version GetPSVersion()
        {
            // In order to read the $PSVersionTable variable,
            // we are forced to create a new runspace to avoid concurrency issues,
            // which is expensive.
            // Rather than do that, we instead go straight to the source,
            // which is a static property, internal in WinPS and public in PS 6+
            return typeof(PSObject).Assembly
                .GetType("System.Management.Automation.PSVersionInfo")
                .GetMethod("get_PSVersion", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(null, new object[0]) as Version;
        }
    }
}
