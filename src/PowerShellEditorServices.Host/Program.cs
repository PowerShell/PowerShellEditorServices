//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
            // Catch unhandled exceptions for logging purposes
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Initialize the logger
            // TODO: Set the level based on command line parameter
            Logger.Initialize(minimumLogLevel: LogLevel.Verbose);
            Logger.Write(LogLevel.Normal, "PowerShell Editor Services Host started!");

            // TODO: Select host, console host, and transport based on command line arguments

            MessageLoop messageLoop = new MessageLoop();
            messageLoop.Start();
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
