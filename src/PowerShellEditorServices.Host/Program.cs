//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Host
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
#if DEBUG
            bool waitForDebugger =
                args.Any(
                    arg => 
                        string.Equals(
                            arg,
                            "/waitForDebugger",
                            StringComparison.InvariantCultureIgnoreCase));

            if (waitForDebugger)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }
            }
#endif

            string logPath = null;
            string logPathArgument =
                args.FirstOrDefault(
                    arg => 
                        arg.StartsWith(
                            "/logPath:",
                            StringComparison.InvariantCultureIgnoreCase));

            if (!string.IsNullOrEmpty(logPathArgument))
            {
                logPath = logPathArgument.Substring(9).Trim('"');
            }

            LogLevel logLevel = LogLevel.Normal;
            string logLevelArgument =
                args.FirstOrDefault(
                    arg => 
                        arg.StartsWith(
                            "/logLevel:",
                            StringComparison.InvariantCultureIgnoreCase));

            if (!string.IsNullOrEmpty(logLevelArgument))
            {
                // Attempt to parse the log level
                Enum.TryParse<LogLevel>(
                    logLevelArgument.Substring(10).Trim('"'),
                    true,
                    out logLevel);
            }

            bool runDebugAdapter =
                args.Any(
                    arg => 
                        string.Equals(
                            arg,
                            "/debugAdapter",
                            StringComparison.InvariantCultureIgnoreCase));

            string hostProfileId = null;
            string hostProfileIdArgument =
                args.FirstOrDefault(
                    arg =>
                        arg.StartsWith(
                            "/hostProfileId:",
                            StringComparison.InvariantCultureIgnoreCase));

            if (!string.IsNullOrEmpty(hostProfileIdArgument))
            {
                hostProfileId = hostProfileIdArgument.Substring(15).Trim('"');
            }

            // Catch unhandled exceptions for logging purposes
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Use a default log path filename if one isn't specified
            logPath =
                runDebugAdapter
                ? logPath ?? "DebugAdapter.log"
                : logPath ?? "EditorServices.log";

            // Start the logger with the specified log path and level
            Logger.Initialize(logPath, logLevel);

            FileVersionInfo fileVersionInfo =
                FileVersionInfo.GetVersionInfo(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "PowerShell Editor Services Host v{0} starting (pid {1}, hostProfileId '{2}')...",
                    fileVersionInfo.FileVersion,
                    Process.GetCurrentProcess().Id,
                    hostProfileId));

            // Create the appropriate server type
            ProtocolEndpoint server =
                runDebugAdapter
                ? (ProtocolEndpoint) new DebugAdapter()
                : (ProtocolEndpoint) new LanguageServer(hostProfileId);

            // Start the server
            server.Start().Wait();
            Logger.Write(LogLevel.Normal, "PowerShell Editor Services Host started!");

            // Wait for the server to finish
            server.WaitForExit();

            Logger.Write(LogLevel.Normal, "PowerShell Editor Services Host exited normally.");
        }

        static void CurrentDomain_UnhandledException(
            object sender, 
            UnhandledExceptionEventArgs e)
        {
            // Log the exception
            Logger.Write(
                LogLevel.Error,
                string.Format(
                    "FATAL UNHANDLED EXCEPTION:\r\n\r\n{0}",
                    e.ExceptionObject.ToString()));
        }
    }
}
