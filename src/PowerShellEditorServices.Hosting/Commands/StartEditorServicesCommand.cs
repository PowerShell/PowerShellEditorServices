﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using SMA = System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.EditorServices.Hosting;
using System.Diagnostics;
using System.Globalization;

#if DEBUG
using System.Threading;
using Debugger = System.Diagnostics.Debugger;
#endif

namespace Microsoft.PowerShell.EditorServices.Commands;

/// <summary>
/// The Start-EditorServices command, the conventional entrypoint for PowerShell Editor Services.
/// </summary>
[Cmdlet(VerbsLifecycle.Start, "EditorServices", DefaultParameterSetName = "NamedPipe")]
public sealed class StartEditorServicesCommand : PSCmdlet
{
    private readonly List<IDisposable> _disposableResources;

    private readonly List<IDisposable> _loggerUnsubscribers;

    private HostLogger _logger;

    // NOTE: Ignore the suggestion to use Environment.ProcessId as it doesn't work for
    // .NET 4.6.2 (for Windows PowerShell), and this won't be caught in CI.
    private static readonly int s_currentPID = Process.GetCurrentProcess().Id;

    public StartEditorServicesCommand()
    {
        // Sets the distribution channel to "PSES" so starts can be distinguished in PS7+ telemetry
        Environment.SetEnvironmentVariable("POWERSHELL_DISTRIBUTION_CHANNEL", "PSES");
        _disposableResources = new List<IDisposable>();
        _loggerUnsubscribers = new List<IDisposable>();
    }

    /// <summary>
    /// The name of the EditorServices host to report.
    /// </summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string HostName { get; set; } = "PSES";

    /// <summary>
    /// The ID to give to the host's profile.
    /// </summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string HostProfileId { get; set; } = "PSES";

    /// <summary>
    /// The version to report for the host.
    /// </summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public Version HostVersion { get; set; } = new Version(0, 0, 0);

    /// <summary>
    /// Path to the session file to create on startup or startup failure.
    /// </summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string SessionDetailsPath { get; set; } = "PowerShellEditorServices.json";

    /// <summary>
    /// The name of the named pipe to use for the LSP transport.
    /// If left unset and named pipes are used as transport, a new name will be generated.
    /// </summary>
    [Parameter(ParameterSetName = "NamedPipe")]
    public string LanguageServicePipeName { get; set; }

    /// <summary>
    /// The name of the named pipe to use for the debug adapter transport.
    /// If left unset and named pipes are used as a transport, a new name will be generated.
    /// </summary>
    [Parameter(ParameterSetName = "NamedPipe")]
    public string DebugServicePipeName { get; set; }

    /// <summary>
    /// The name of the input named pipe to use for the LSP transport.
    /// </summary>
    [Parameter(ParameterSetName = "NamedPipeSimplex")]
    public string LanguageServiceInPipeName { get; set; }

    /// <summary>
    /// The name of the output named pipe to use for the LSP transport.
    /// </summary>
    [Parameter(ParameterSetName = "NamedPipeSimplex")]
    public string LanguageServiceOutPipeName { get; set; }

    /// <summary>
    /// The name of the input pipe to use for the debug adapter transport.
    /// </summary>
    [Parameter(ParameterSetName = "NamedPipeSimplex")]
    public string DebugServiceInPipeName { get; set; }

    /// <summary>
    /// The name of the output pipe to use for the debug adapter transport.
    /// </summary>
    [Parameter(ParameterSetName = "NamedPipeSimplex")]
    public string DebugServiceOutPipeName { get; set; }

    /// <summary>
    /// If set, uses standard input/output as the LSP transport.
    /// When <see cref="DebugServiceOnly"/> is set with this, standard input/output
    /// is used as the debug adapter transport.
    /// </summary>
    [Parameter(ParameterSetName = "Stdio")]
    public SwitchParameter Stdio { get; set; }

    /// <summary>
    /// The path to where PowerShellEditorServices and its bundled modules are.
    /// </summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string BundledModulesPath { get; set; } = Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(typeof(StartEditorServicesCommand).Assembly.Location),
        "..", "..", ".."));

    /// <summary>
    /// The absolute path to the folder where logs will be saved.
    /// </summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string LogPath { get; set; } = Path.Combine(Path.GetTempPath(), "PowerShellEditorServices");

    /// <summary>
    /// The minimum log level that should be emitted.
    /// </summary>
    [Parameter]
    public string LogLevel { get; set; } = PsesLogLevel.Warning.ToString();

    /// <summary>
    /// Paths to additional PowerShell modules to be imported at startup.
    /// </summary>
    [Parameter]
    public string[] AdditionalModules { get; set; }

    /// <summary>
    /// Any feature flags to enable in EditorServices.
    /// </summary>
    [Parameter]
    public string[] FeatureFlags { get; set; }

    /// <summary>
    /// When set, enables the Extension Terminal.
    /// </summary>
    [Parameter]
    public SwitchParameter EnableConsoleRepl { get; set; }

    /// <summary>
    /// When set and the console is enabled, the legacy lightweight
    /// readline implementation will be used instead of PSReadLine.
    /// </summary>
    [Parameter]
    public SwitchParameter UseLegacyReadLine { get; set; }

    /// <summary>
    /// When set, do not enable LSP service, only the debug adapter.
    /// </summary>
    [Parameter]
    public SwitchParameter DebugServiceOnly { get; set; }

    /// <summary>
    /// When set, do not enable debug adapter, only the language service.
    /// </summary>
    [Parameter]
    public SwitchParameter LanguageServiceOnly { get; set; }

    /// <summary>
    /// When set with a debug build, startup will wait for a debugger to attach.
    /// </summary>
    [Parameter]
    public SwitchParameter WaitForDebugger { get; set; }

    /// <summary>
    /// When set, will generate two simplex named pipes using a single named pipe name.
    /// </summary>
    [Parameter]
    public SwitchParameter SplitInOutPipes { get; set; }

    /// <summary>
    /// The banner/logo to display when the extension terminal is first started.
    /// </summary>
    [Parameter]
    public string StartupBanner { get; set; }

    /// <summary>
    /// Compatibility to store the currently supported PSESLogLevel Enum Value
    /// </summary>
    private PsesLogLevel _psesLogLevel = PsesLogLevel.Warning;

#pragma warning disable IDE0022
    protected override void BeginProcessing()
    {
#if DEBUG
        if (WaitForDebugger)
        {
            // NOTE: Ignore the suggestion to use Environment.ProcessId as it doesn't work for
            // .NET 4.6.2 (for Windows PowerShell), and this won't be caught in CI.
            Console.WriteLine($"Waiting for debugger to attach, PID: {s_currentPID}");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(1000);
            }
        }
#endif
        // Set up logging now for use throughout startup
        StartLogging();
    }
#pragma warning restore IDE0022

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "We have to wait here, it's the whole program.")]
    protected override void EndProcessing()
    {
        _logger.Log(PsesLogLevel.Trace, "Beginning EndProcessing block");
        try
        {
            // First try to remove PSReadLine to decomplicate startup
            // If PSReadLine is enabled, it will be re-imported later
            RemovePSReadLineForStartup();

            // Create the configuration from parameters
            EditorServicesConfig editorServicesConfig = CreateConfigObject();

            using EditorServicesLoader psesLoader = EditorServicesLoader.Create(_logger, editorServicesConfig, SessionDetailsPath, _loggerUnsubscribers);
            _logger.Log(PsesLogLevel.Debug, "Loading EditorServices");
            // Synchronously start editor services and wait here until it shuts down.
            psesLoader.LoadAndRunEditorServicesAsync().GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            _logger.LogException("Exception encountered starting EditorServices", e);

            // Give the user a chance to read the message if they have a console
            if (!Stdio)
            {
                Host.UI.WriteLine("\n== Press any key to close terminal ==");
                Console.ReadKey();
            }

            ThrowTerminatingError(new ErrorRecord(e, "PowerShellEditorServicesError", ErrorCategory.NotSpecified, this));
        }
        finally
        {
            foreach (IDisposable disposableResource in _disposableResources)
            {
                disposableResource.Dispose();
            }
        }
    }

    private void StartLogging()
    {
        bool isLegacyPsesLogLevel = false;
        if (!Enum.TryParse(LogLevel, true, out _psesLogLevel))
        {
            // PSES used to have log levels that didn't match MEL levels, this is an adapter for those types and may eventually be removed once people migrate their settings.
            isLegacyPsesLogLevel = true;
            _psesLogLevel = LogLevel switch
            {
                "Diagnostic" => PsesLogLevel.Trace,
                "Verbose" => PsesLogLevel.Debug,
                "Normal" => PsesLogLevel.Information,
                _ => PsesLogLevel.Trace
            };
        }

        _logger = new HostLogger(_psesLogLevel);
        if (isLegacyPsesLogLevel)
        {
            _logger.Log(PsesLogLevel.Warning, $"The log level '{LogLevel}' is deprecated and will be removed in a future release. Please update your settings or command line options to use one of the following options: 'Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'.");
        }

        // We need to not write log messages to Stdio
        // if it's being used as a protocol transport
        if (!Stdio)
        {
            PSHostLogger hostLogger = new(Host.UI);
            _loggerUnsubscribers.Add(_logger.Subscribe(hostLogger));
        }

        string logDirPath = GetLogDirPath();
        string logPath = Path.Combine(logDirPath, $"StartEditorServices-{s_currentPID}.log");

        if (File.Exists(logPath))
        {
            int randomInt = new Random().Next();
            logPath = Path.Combine(logDirPath, $"StartEditorServices-{s_currentPID}-{randomInt.ToString("X", CultureInfo.InvariantCulture.NumberFormat)}.log");
        }

        StreamLogger fileLogger = StreamLogger.CreateWithNewFile(logPath);
        _disposableResources.Add(fileLogger);
        IDisposable fileLoggerUnsubscriber = _logger.Subscribe(fileLogger);
        fileLogger.AddUnsubscriber(fileLoggerUnsubscriber);
        _loggerUnsubscribers.Add(fileLoggerUnsubscriber);
        _logger.Log(PsesLogLevel.Trace, "Logging started");
    }

    // Sanitizes user input and ensures the directory is created.
    private string GetLogDirPath()
    {
        string logDir = LogPath;
        if (string.IsNullOrEmpty(logDir))
        {
            logDir = Path.Combine(Path.GetTempPath(), "PowerShellEditorServices");
        }

        Directory.CreateDirectory(logDir);
        return logDir;
    }

    private void RemovePSReadLineForStartup()
    {
        _logger.Log(PsesLogLevel.Debug, "Removing PSReadLine");
        using SMA.PowerShell pwsh = SMA.PowerShell.Create(RunspaceMode.CurrentRunspace);
        bool hasPSReadLine = pwsh.AddCommand(new CmdletInfo(@"Microsoft.PowerShell.Core\Get-Module", typeof(GetModuleCommand)))
            .AddParameter("Name", "PSReadLine")
            .Invoke()
            .Count > 0;

        if (hasPSReadLine)
        {
            pwsh.Commands.Clear();

            pwsh.AddCommand(new CmdletInfo(@"Microsoft.PowerShell.Core\Remove-Module", typeof(RemoveModuleCommand)))
                .AddParameter("Name", "PSReadLine")
                .AddParameter("ErrorAction", "SilentlyContinue");

            _logger.Log(PsesLogLevel.Debug, "Removed PSReadLine");
        }
    }

    private EditorServicesConfig CreateConfigObject()
    {
        _logger.Log(PsesLogLevel.Trace, "Creating host configuration");

        string bundledModulesPath = BundledModulesPath;
        if (!Path.IsPathRooted(bundledModulesPath))
        {
            // For compatibility, the bundled modules path is relative to the PSES bin directory
            // Ideally it should be one level up, the PSES module root
            bundledModulesPath = Path.GetFullPath(
                Path.Combine(
                    Assembly.GetExecutingAssembly().Location,
                    "..",
                    bundledModulesPath));
        }

        PSObject profile = (PSObject)GetVariableValue("profile");

        HostInfo hostInfo = new(HostName, HostProfileId, HostVersion);

        InitialSessionState initialSessionState = Runspace.DefaultRunspace.InitialSessionState;
        initialSessionState.LanguageMode = Runspace.DefaultRunspace.SessionStateProxy.LanguageMode;

        EditorServicesConfig editorServicesConfig = new(
            hostInfo,
            Host,
            SessionDetailsPath,
            bundledModulesPath,
            LogPath)
        {
            FeatureFlags = FeatureFlags,
            LogLevel = _psesLogLevel,
            ConsoleRepl = GetReplKind(),
            UseNullPSHostUI = Stdio, // If Stdio is used we can't write anything else out
            AdditionalModules = AdditionalModules,
            LanguageServiceTransport = GetLanguageServiceTransport(),
            DebugServiceTransport = GetDebugServiceTransport(),
            InitialSessionState = initialSessionState,
            ProfilePaths = new ProfilePathConfig
            {
                AllUsersAllHosts = GetProfilePathFromProfileObject(profile, ProfileUserKind.AllUsers, ProfileHostKind.AllHosts),
                AllUsersCurrentHost = GetProfilePathFromProfileObject(profile, ProfileUserKind.AllUsers, ProfileHostKind.CurrentHost),
                CurrentUserAllHosts = GetProfilePathFromProfileObject(profile, ProfileUserKind.CurrentUser, ProfileHostKind.AllHosts),
                CurrentUserCurrentHost = GetProfilePathFromProfileObject(profile, ProfileUserKind.CurrentUser, ProfileHostKind.CurrentHost),
            },
        };

        if (StartupBanner != null)
        {
            editorServicesConfig.StartupBanner = StartupBanner;
        }

        return editorServicesConfig;
    }

    private string GetProfilePathFromProfileObject(PSObject profileObject, ProfileUserKind userKind, ProfileHostKind hostKind)
    {
        string profilePathName = $"{userKind}{hostKind}";
        if (profileObject is null)
        {
            return null;
        }
        string pwshProfilePath = (string)profileObject.Properties[profilePathName].Value;

        if (hostKind == ProfileHostKind.AllHosts)
        {
            return pwshProfilePath;
        }

        return Path.Combine(
            Path.GetDirectoryName(pwshProfilePath),
            $"{HostProfileId}_profile.ps1");
    }

    // We should only use PSReadLine if we specified that we want a console repl
    // and we have not explicitly said to use the legacy ReadLine.
    // We also want it if we are either:
    // * On Windows on any version OR
    // * On Linux or macOS on any version greater than or equal to 7
    private ConsoleReplKind GetReplKind()
    {
        _logger.Log(PsesLogLevel.Trace, "Determining REPL kind");

        if (Stdio || !EnableConsoleRepl)
        {
            _logger.Log(PsesLogLevel.Trace, "REPL configured as None");
            return ConsoleReplKind.None;
        }

        if (UseLegacyReadLine)
        {
            _logger.Log(PsesLogLevel.Trace, "REPL configured as Legacy");
            return ConsoleReplKind.LegacyReadLine;
        }

        _logger.Log(PsesLogLevel.Trace, "REPL configured as PSReadLine");
        return ConsoleReplKind.PSReadLine;
    }

    private ITransportConfig GetLanguageServiceTransport()
    {
        _logger.Log(PsesLogLevel.Trace, "Configuring LSP transport");

        if (DebugServiceOnly)
        {
            _logger.Log(PsesLogLevel.Trace, "No LSP transport: PSES is debug only");
            return null;
        }

        if (Stdio)
        {
            return new StdioTransportConfig(_logger);
        }

        if (LanguageServiceInPipeName != null && LanguageServiceOutPipeName != null)
        {
            return SimplexNamedPipeTransportConfig.Create(_logger, LanguageServiceInPipeName, LanguageServiceOutPipeName);
        }

        if (SplitInOutPipes)
        {
            return SimplexNamedPipeTransportConfig.Create(_logger, LanguageServicePipeName);
        }

        return DuplexNamedPipeTransportConfig.Create(_logger, LanguageServicePipeName);
    }

    private ITransportConfig GetDebugServiceTransport()
    {
        _logger.Log(PsesLogLevel.Trace, "Configuring debug transport");

        if (LanguageServiceOnly)
        {
            _logger.Log(PsesLogLevel.Trace, "No Debug transport: PSES is language service only");
            return null;
        }

        if (Stdio)
        {
            if (DebugServiceOnly)
            {
                return new StdioTransportConfig(_logger);
            }

            _logger.Log(PsesLogLevel.Trace, "No debug transport: Transport is Stdio with debug disabled");
            return null;
        }

        if (DebugServiceInPipeName != null && DebugServiceOutPipeName != null)
        {
            return SimplexNamedPipeTransportConfig.Create(_logger, DebugServiceInPipeName, DebugServiceOutPipeName);
        }

        if (SplitInOutPipes)
        {
            return SimplexNamedPipeTransportConfig.Create(_logger, DebugServicePipeName);
        }

        return DuplexNamedPipeTransportConfig.Create(_logger, DebugServicePipeName);
    }

    private enum ProfileHostKind
    {
        AllHosts,
        CurrentHost,
    }

    private enum ProfileUserKind
    {
        AllUsers,
        CurrentUser,
    }
}
