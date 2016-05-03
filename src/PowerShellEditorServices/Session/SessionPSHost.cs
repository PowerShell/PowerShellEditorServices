﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides an implementation of the PSHost class for the
    /// ConsoleService and routes its calls to an IConsoleHost
    /// implementation.
    /// </summary>
    internal class ConsoleServicePSHost : PSHost, IHostSupportsInteractiveSession
    {
        #region Private Fields

        private HostDetails hostDetails;
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
        /// <param name="hostDetails">
        /// Provides details about the host application.
        /// </param>
        public ConsoleServicePSHost(HostDetails hostDetails)
        {
            this.hostDetails = hostDetails;
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
            get { return this.hostDetails.Name; }
        }

        public override Version Version
        {
            get { return this.hostDetails.Version; }
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

        public bool IsRunspacePushed
        {
            get { return false; }
        }

        public Runspace Runspace
        {
            get; internal set;
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

        public void PushRunspace(Runspace runspace)
        {
            throw new NotImplementedException();
        }

        public void PopRunspace()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
