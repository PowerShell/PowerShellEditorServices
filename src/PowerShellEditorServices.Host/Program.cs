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
using System.Threading;

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

            // Should we wait for the debugger before starting?
            if (waitForDebugger)
            {
                // Wait for 25 seconds and then continue
                int waitCountdown = 25;
                while (!Debugger.IsAttached && waitCountdown > 0)
                {
                    Thread.Sleep(1000);
                    waitCountdown--;
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

            // Catch unhandled exceptions for logging purposes
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            ProtocolEndpoint server = null;
            if (runDebugAdapter)
            {
                logPath = logPath ?? "DebugAdapter.log";
                server = new DebugAdapter();
            }
            else
            {
                logPath = logPath ?? "EditorServices.log";
                server = new LanguageServer();
            }

            // Start the logger with the specified log path and level
            Logger.Initialize(logPath, logLevel);

            FileVersionInfo fileVersionInfo =
                FileVersionInfo.GetVersionInfo(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "PowerShell Editor Services Host v{0} starting...",
                    fileVersionInfo.FileVersion));

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
