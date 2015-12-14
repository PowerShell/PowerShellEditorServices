//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;

namespace Microsoft.PowerShell.EditorServices.Host
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
#if DEBUG
            // In the future, a more robust argument parser will be added here
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
                // Wait for 15 seconds and then continue
                int waitCountdown = 15;
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

            bool runDebugAdapter =
                args.Any(
                    arg => 
                        string.Equals(
                            arg,
                            "/debugAdapter",
                            StringComparison.InvariantCultureIgnoreCase));

            string websocketPortString =
                args.FirstOrDefault(
                    arg =>
                        arg.StartsWith(
                            "/websockets:",
                            StringComparison.InvariantCultureIgnoreCase));

            int websocketPort = -1;
            if (!string.IsNullOrEmpty(websocketPortString))
            {
                websocketPortString = websocketPortString.Split(':')[1].Trim('"');
                if (!int.TryParse(websocketPortString, out websocketPort))
                {
                    websocketPort = -1;
                }
            }

            // Catch unhandled exceptions for logging purposes
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            ProtocolServer server = null;
            if (runDebugAdapter)
            {
                logPath = logPath ?? "DebugAdapter.log";
                server = new DebugAdapter();
            }
            else
            {
                logPath = logPath ?? "EditorServices.log";

                if (websocketPort != -1)
                {
                    server = new LanguageServer(new WebsocketServerChannel(websocketPort));
                }
                else
                {
                    server = new LanguageServer();
                }
            }

            // Start the logger with the specified log path
            // TODO: Set the level based on command line parameter
            Logger.Initialize(logPath, LogLevel.Verbose);

            Logger.Write(LogLevel.Normal, "PowerShell Editor Services Host starting...");

            Logger.Write(LogLevel.Normal, "WebsocketPort:" + websocketPort);

            // Start the server
            server.Start();
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
