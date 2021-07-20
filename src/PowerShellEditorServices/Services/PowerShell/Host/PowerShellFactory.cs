using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    using Microsoft.Extensions.Logging;
    using Microsoft.PowerShell.EditorServices.Hosting;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
    using Microsoft.PowerShell.EditorServices.Utility;
    using System.Collections.Generic;
    using System.IO;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Reflection;

    internal class PowerShellFactory
    {
        private static readonly string s_commandsModulePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "../../Commands/PowerShellEditorServices.Commands.psd1"));

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly InternalHost _psesHost;

        public PowerShellFactory(
            ILoggerFactory loggerFactory,
            InternalHost psHost)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PowerShellFactory>();
            _psesHost = psHost;
        }

        public PowerShell CreateNestedPowerShell(RunspaceInfo currentRunspace)
        {
            if (currentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                var remotePwsh = PowerShell.Create();
                remotePwsh.Runspace = currentRunspace.Runspace;
                return remotePwsh;
            }

            // PowerShell.CreateNestedPowerShell() sets IsNested but not IsChild
            // This means it throws due to the parent pipeline not running...
            // So we must use the RunspaceMode.CurrentRunspace option on PowerShell.Create() instead
            var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
            pwsh.Runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            return pwsh;
        }

        public PowerShell CreatePowerShellForRunspace(Runspace runspace)
        {
            var pwsh = PowerShell.Create();
            pwsh.Runspace = runspace;
            return pwsh;
        }

        public PowerShell CreateInitialPowerShell(
            HostStartupInfo hostStartupInfo,
            ReadLineProvider readLineProvider)
        {
            Runspace runspace = CreateInitialRunspace(hostStartupInfo.LanguageMode);

            var pwsh = PowerShell.Create();
            pwsh.Runspace = runspace;

            var engineIntrinsics = (EngineIntrinsics)runspace.SessionStateProxy.GetVariable("ExecutionContext");

            if (hostStartupInfo.ConsoleReplEnabled && !hostStartupInfo.UsesLegacyReadLine)
            {
                var psrlProxy = PSReadLineProxy.LoadAndCreate(_loggerFactory, pwsh);
                var readLine = new ConsoleReadLine(psrlProxy, _psesHost, _psesHost.ExecutionService, engineIntrinsics);
                readLineProvider.OverrideReadLine(readLine);
            }

            if (VersionUtils.IsWindows)
            {
                pwsh.SetCorrectExecutionPolicy(_logger);
            }

            pwsh.ImportModule(s_commandsModulePath);

            if (hostStartupInfo.AdditionalModules != null && hostStartupInfo.AdditionalModules.Count > 0)
            {
                foreach (string module in hostStartupInfo.AdditionalModules)
                {
                    pwsh.ImportModule(module);
                }
            }

            return pwsh;
        }

        private Runspace CreateInitialRunspace(PSLanguageMode languageMode)
        {
            InitialSessionState iss = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                ? InitialSessionState.CreateDefault()
                : InitialSessionState.CreateDefault2();

            iss.LanguageMode = languageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(_psesHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            runspace.Open();

            Runspace.DefaultRunspace = runspace;

            return runspace;
        }

    }
}
