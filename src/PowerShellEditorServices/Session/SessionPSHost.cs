//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides an implementation of the PSHost class for the
    /// ConsoleService and routes its calls to an IConsoleHost
    /// implementation.
    /// </summary>
    internal class ConsoleServicePSHost : PSHost
    {
        #region Private Fields

        private IConsoleHost consoleHost;
        private Guid instanceId = Guid.NewGuid();
        private ConsoleServicePSHostUserInterface hostUserInterface;

        #endregion

        #region Properties

        internal IConsoleHost ConsoleHost
        {
            get { return this.consoleHost; }
            set
            {
                this.consoleHost = value;
                this.hostUserInterface.ConsoleHost = value;
            }
        }

        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHost class
        /// with the given IConsoleHost implementation.
        /// </summary>
        public ConsoleServicePSHost()
        {
            this.hostUserInterface = new ConsoleServicePSHostUserInterface();
        }

        #endregion

        #region PSHost Implementation

        public override Guid InstanceId
        {
            get { return this.instanceId; }
        }

        public override string Name
        {
            // TODO: Change this based on proper naming!
            get { return "PowerShell Editor Services"; }
        }

        public override Version Version
        {
            get
            {
                return this.GetType().Assembly.GetName().Version;
            }
        }

        // TODO: Pull these from IConsoleHost

        public override System.Globalization.CultureInfo CurrentCulture
        {
            get { return System.Threading.Thread.CurrentThread.CurrentCulture; }
        }

        public override System.Globalization.CultureInfo CurrentUICulture
        {
            get { return System.Threading.Thread.CurrentThread.CurrentUICulture; }
        }

        public override PSHostUserInterface UI
        {
            get { return this.hostUserInterface; }
        }

        public override void EnterNestedPrompt()
        {
            Logger.Write(LogLevel.Verbose, "EnterNestedPrompt() called.");
        }

        public override void ExitNestedPrompt()
        {
            Logger.Write(LogLevel.Verbose, "ExitNestedPrompt() called.");
        }

        public override void NotifyBeginApplication()
        {
            Logger.Write(LogLevel.Verbose, "NotifyBeginApplication() called.");
        }

        public override void NotifyEndApplication()
        {
            Logger.Write(LogLevel.Verbose, "NotifyEndApplication() called.");
        }

        public override void SetShouldExit(int exitCode)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.ExitSession(exitCode);
            }
        }

        #endregion
    }
}
