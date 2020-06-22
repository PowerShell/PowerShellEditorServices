using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    internal class PowerShellStartupService
    {
        public static PowerShellStartupService Create(
            ILogger logger,
            HostStartupInfo hostStartupInfo)
        {
            var pwsh = SMA.PowerShell.Create();

            var readLine = new ConsoleReadLine();

            var rawUI = new EditorServicesConsolePSHostRawUserInterface(logger, hostStartupInfo.PSHost.UI.RawUI);

            var ui = new EditorServicesConsolePSHostUserInterface(logger, rawUI, readLine, hostStartupInfo.PSHost.UI);

            var psHost = new EditorServicesConsolePSHost(logger, ui, hostStartupInfo.Name, hostStartupInfo.Version);

            pwsh.Runspace = CreateRunspace(psHost, hostStartupInfo.LanguageMode);
            Runspace.DefaultRunspace = pwsh.Runspace;

            var engineIntrinsics = (EngineIntrinsics)pwsh.Runspace.SessionStateProxy.GetVariable("ExecutionContext");

            readLine.RegisterPSReadLineProxy(PSReadLineProxy.LoadAndCreate(logger, pwsh));
            readLine.RegisterPowerShellEngine(psHost, engineIntrinsics);

            return new PowerShellStartupService(pwsh, engineIntrinsics, psHost, readLine);
        }

        private PowerShellStartupService(
            SMA.PowerShell pwsh,
            EngineIntrinsics engineIntrinsics,
            EditorServicesConsolePSHost editorServicesHost,
            ConsoleReadLine readLine)
        {
            PowerShell = pwsh;
            EngineIntrinsics = engineIntrinsics;
            EditorServicesHost = editorServicesHost;
            ReadLine = readLine;
        }

        public SMA.PowerShell PowerShell { get; }

        public EngineIntrinsics EngineIntrinsics { get; }

        public EditorServicesConsolePSHost EditorServicesHost { get; }

        public ConsoleReadLine ReadLine { get; }

        private static Runspace CreateRunspace(
            PSHost psHost,
            PSLanguageMode languageMode)
        {
            InitialSessionState iss = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                ? InitialSessionState.CreateDefault()
                : InitialSessionState.CreateDefault2();

            iss.LanguageMode = languageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.ReuseThread;

            runspace.Open();

            return runspace;
        }
    }
}
