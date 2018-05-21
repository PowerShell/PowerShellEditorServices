﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides an implementation of the PSHost class for the
    /// ConsoleService and routes its calls to an IConsoleHost
    /// implementation.
    /// </summary>
    public class EditorServicesPSHost : PSHost, IHostSupportsInteractiveSession
    {
        #region Private Fields

        private ILogger Logger;
        private HostDetails hostDetails;
        private Guid instanceId = Guid.NewGuid();
        private EditorServicesPSHostUserInterface hostUserInterface;
        private IHostSupportsInteractiveSession hostSupportsInteractiveSession;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHost class
        /// with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="powerShellContext">
        /// An implementation of IHostSupportsInteractiveSession for runspace management.
        /// </param>
        /// <param name="hostDetails">
        /// Provides details about the host application.
        /// </param>
        /// <param name="hostUserInterface">
        /// The EditorServicesPSHostUserInterface implementation to use for this host.
        /// </param>
        /// <param name="logger">An ILogger implementation to use for this host.</param>
        public EditorServicesPSHost(
            PowerShellContext powerShellContext,
            HostDetails hostDetails,
            EditorServicesPSHostUserInterface hostUserInterface,
            ILogger logger)
        {
            this.Logger = logger;
            this.hostDetails = hostDetails;
            this.hostUserInterface = hostUserInterface;
            this.hostSupportsInteractiveSession = powerShellContext;
        }

        #endregion

        #region PSHost Implementation

        /// <summary>
        ///
        /// </summary>
        public override Guid InstanceId
        {
            get { return this.instanceId; }
        }

        /// <summary>
        ///
        /// </summary>
        public override string Name
        {
            get { return this.hostDetails.Name; }
        }

        internal class ConsoleColorProxy
        {
            private EditorServicesPSHostUserInterface _hostUserInterface;

            internal ConsoleColorProxy(EditorServicesPSHostUserInterface hostUserInterface)
            {
                if (hostUserInterface == null) throw new ArgumentNullException("hostUserInterface");
                _hostUserInterface = hostUserInterface;
            }

            /// <summary>
            /// The ForegroundColor for Error
            /// </summary>
            public ConsoleColor ErrorForegroundColor
            {
                get
                { return _hostUserInterface.ErrorForegroundColor; }
                set
                { _hostUserInterface.ErrorForegroundColor = value; }
            }

            /// <summary>
            /// The BackgroundColor for Error
            /// </summary>
            public ConsoleColor ErrorBackgroundColor
            {
                get
                { return _hostUserInterface.ErrorBackgroundColor; }
                set
                { _hostUserInterface.ErrorBackgroundColor = value; }
            }

            /// <summary>
            /// The ForegroundColor for Warning
            /// </summary>
            public ConsoleColor WarningForegroundColor
            {
                get
                { return _hostUserInterface.WarningForegroundColor; }
                set
                { _hostUserInterface.WarningForegroundColor = value; }
            }

            /// <summary>
            /// The BackgroundColor for Warning
            /// </summary>
            public ConsoleColor WarningBackgroundColor
            {
                get
                { return _hostUserInterface.WarningBackgroundColor; }
                set
                { _hostUserInterface.WarningBackgroundColor = value; }
            }

            /// <summary>
            /// The ForegroundColor for Debug
            /// </summary>
            public ConsoleColor DebugForegroundColor
            {
                get
                { return _hostUserInterface.DebugForegroundColor; }
                set
                { _hostUserInterface.DebugForegroundColor = value; }
            }

            /// <summary>
            /// The BackgroundColor for Debug
            /// </summary>
            public ConsoleColor DebugBackgroundColor
            {
                get
                { return _hostUserInterface.DebugBackgroundColor; }
                set
                { _hostUserInterface.DebugBackgroundColor = value; }
            }

            /// <summary>
            /// The ForegroundColor for Verbose
            /// </summary>
            public ConsoleColor VerboseForegroundColor
            {
                get
                { return _hostUserInterface.VerboseForegroundColor; }
                set
                { _hostUserInterface.VerboseForegroundColor = value; }
            }

            /// <summary>
            /// The BackgroundColor for Verbose
            /// </summary>
            public ConsoleColor VerboseBackgroundColor
            {
                get
                { return _hostUserInterface.VerboseBackgroundColor; }
                set
                { _hostUserInterface.VerboseBackgroundColor = value; }
            }

            /// <summary>
            /// The ForegroundColor for Progress
            /// </summary>
            public ConsoleColor ProgressForegroundColor
            {
                get
                { return _hostUserInterface.ProgressForegroundColor; }
                set
                { _hostUserInterface.ProgressForegroundColor = value; }
            }

            /// <summary>
            /// The BackgroundColor for Progress
            /// </summary>
            public ConsoleColor ProgressBackgroundColor
            {
                get
                { return _hostUserInterface.ProgressBackgroundColor; }
                set
                { _hostUserInterface.ProgressBackgroundColor = value; }
            }
        }

        /// <summary>
        /// Return the actual console host object so that the user can get at
        /// the unproxied methods.
        /// </summary>
        public override PSObject PrivateData
        {
            get
            {
                if (hostUserInterface == null) return null;
                return _consoleColorProxy ?? (_consoleColorProxy = PSObject.AsPSObject(new ConsoleColorProxy(hostUserInterface)));
            }
        }
        private PSObject _consoleColorProxy;

        /// <summary>
        ///
        /// </summary>
        public override Version Version
        {
            get { return this.hostDetails.Version; }
        }

        // TODO: Pull these from IConsoleHost

        /// <summary>
        ///
        /// </summary>
        public override System.Globalization.CultureInfo CurrentCulture
        {
            get { return System.Globalization.CultureInfo.CurrentCulture; }
        }

        /// <summary>
        ///
        /// </summary>
        public override System.Globalization.CultureInfo CurrentUICulture
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture; }
        }

        /// <summary>
        ///
        /// </summary>
        public override PSHostUserInterface UI
        {
            get { return this.hostUserInterface; }
        }

        /// <summary>
        ///
        /// </summary>
        public override void EnterNestedPrompt()
        {
            Logger.Write(LogLevel.Verbose, "EnterNestedPrompt() called.");
        }

        /// <summary>
        ///
        /// </summary>
        public override void ExitNestedPrompt()
        {
            Logger.Write(LogLevel.Verbose, "ExitNestedPrompt() called.");
        }

        /// <summary>
        ///
        /// </summary>
        public override void NotifyBeginApplication()
        {
            Logger.Write(LogLevel.Verbose, "NotifyBeginApplication() called.");
            this.hostUserInterface.IsNativeApplicationRunning = true;
        }

        /// <summary>
        ///
        /// </summary>
        public override void NotifyEndApplication()
        {
            Logger.Write(LogLevel.Verbose, "NotifyEndApplication() called.");
            this.hostUserInterface.IsNativeApplicationRunning = false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="exitCode"></param>
        public override void SetShouldExit(int exitCode)
        {
            if (this.IsRunspacePushed)
            {
                this.PopRunspace();
            }
        }

        #endregion

        #region IHostSupportsInteractiveSession Implementation

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool IsRunspacePushed
        {
            get
            {
                if (this.hostSupportsInteractiveSession != null)
                {
                    return this.hostSupportsInteractiveSession.IsRunspacePushed;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public Runspace Runspace
        {
            get
            {
                if (this.hostSupportsInteractiveSession != null)
                {
                    return this.hostSupportsInteractiveSession.Runspace;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="runspace"></param>
        public void PushRunspace(Runspace runspace)
        {
            if (this.hostSupportsInteractiveSession != null)
            {
                this.hostSupportsInteractiveSession.PushRunspace(runspace);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void PopRunspace()
        {
            if (this.hostSupportsInteractiveSession != null)
            {
                this.hostSupportsInteractiveSession.PopRunspace();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    }
}
