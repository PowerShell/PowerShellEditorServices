//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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

        #region Constructors
        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHost class
        /// with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost that will be used to perform host actions for this class.
        /// </param>
        public ConsoleServicePSHost(IConsoleHost consoleHost)
        {
            this.consoleHost = consoleHost;
            this.hostUserInterface = new ConsoleServicePSHostUserInterface(consoleHost);
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
            // TODO: Pull this from the host application
            get { return new Version("0.1.0"); }
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
            throw new NotImplementedException();
        }

        public override void ExitNestedPrompt()
        {
            throw new NotImplementedException();
        }

        public override void NotifyBeginApplication()
        {
            throw new NotImplementedException();
        }

        public override void NotifyEndApplication()
        {
            throw new NotImplementedException();
        }

        public override void SetShouldExit(int exitCode)
        {
            this.consoleHost.ExitSession(exitCode);
        }

        #endregion
    }
}
